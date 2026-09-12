using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS EDMS Phase 4 Verification» یافته P4-04: هنگام تایید مستقل این‌که SoD واقعاً روی
/// همه‌ی مسیرهای Approve اعمال می‌شود، مشخص شد DocumentWorkflowController یک مسیر دوم و کاملاً
/// بدون بررسی مجوز/SoD برای همان EngineeringDocumentsController.ApproveAsync بود - در
/// DocumentWorkflow/Index.cshtml هم واقعاً به کاربر نمایش داده می‌شد. این تست‌ها دقیقاً همان
/// سناریوی خودتاییدی را روی این کنترلر دوم بازتولید می‌کنند تا ثابت شود دیگر ممکن نیست.
/// </summary>
public class DocumentWorkflowControllerApprovalAuthorizationTests : EngineeringWorkflowTestBase
{
    private (DocumentWorkflowController controller, PermissionService permissionService, AccessControlService accessControl) BuildController(FakeCurrentUserService currentUser)
    {
        var accessControl = new AccessControlService(Ctx);
        var permissionService = new PermissionService(Ctx, accessControl);
        var controller = new DocumentWorkflowController(Ctx, WorkflowService, currentUser, permissionService, accessControl).WithTempData();
        return (controller, permissionService, accessControl);
    }

    private async Task SeedApprovePermissionAndSoDPolicyAsync()
    {
        var permission = new Permission { Name = "EngineeringDocuments.Approve", ResourceName = "EngineeringDocuments", ActionName = "Approve", IsActive = true };
        Ctx.Permissions.Add(permission);
        await Ctx.SaveChangesAsync();

        var role = Ctx.Roles.First(r => r.Name == SystemRoles.EngineeringManager);
        Ctx.RolePermissionGrants.Add(new RolePermissionGrant { RoleId = role.Id, PermissionId = permission.PermissionId, IsActive = true });
        await Ctx.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(Ctx);
        var provider = services.BuildServiceProvider();
        // این متد خودش هم ردیف‌های Permission برای "EngineeringDocuments.Create"/"Edit" می‌سازد
        // (چون SoDPolicy به آن‌ها ارجاع می‌دهد) - اما آن‌ها را به هیچ نقشی Grant نمی‌کند
        await AccessControlSeeder.SeedPhase5GovernanceAsync(provider);

        // طبق تشخیص دقیق رفتار DecideForUserAsync: AuthorizeAsync فقط یک Action "Allow"شده را
        // در AuthorizationAuditLog ثبت می‌کند - پس برای این‌که سناریوی خودتاییدی واقعاً قابل شبیه‌سازی
        // باشد، خودِ Create/Edit هم باید واقعاً مجاز باشند (دقیقاً مثل دیتابیس واقعی که
        // RolePermissions از قبل CanCreate/CanEdit=true برای EngineeringManager دارد)
        foreach (var actionName in new[] { "Create", "Edit" })
        {
            var p = await Ctx.Permissions.FirstAsync(x => x.ResourceName == "EngineeringDocuments" && x.ActionName == actionName);
            Ctx.RolePermissionGrants.Add(new RolePermissionGrant { RoleId = role.Id, PermissionId = p.PermissionId, IsActive = true });
        }
        await Ctx.SaveChangesAsync();
    }

    private async Task<(EngineeringDocument doc, PEMS.Persistence.Identity.ApplicationUser user)> CreateCheckedDocumentAsync(string username)
    {
        var doc = CreateDraftDocument(DisciplineMech.DisciplineId);
        await WorkflowService.SubmitAsync(doc.DocumentId, "submit", 1, ManagerRole);
        await WorkflowService.CheckAsync(doc.DocumentId, "checked", 1, ManagerRole);

        var user = new PEMS.Persistence.Identity.ApplicationUser { UserName = username, Email = $"{username}@pems.local", FullName = username };
        Ctx.Users.Add(user);
        await Ctx.SaveChangesAsync();

        var role = Ctx.Roles.First(r => r.Name == SystemRoles.EngineeringManager);
        Ctx.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<int> { UserId = user.Id, RoleId = role.Id });
        await Ctx.SaveChangesAsync();

        return (doc, user);
    }

    [Fact]
    public async Task Approve_UserWithoutPermission_ReturnsForbid()
    {
        await SeedApprovePermissionAndSoDPolicyAsync();
        var (doc, _) = await CreateCheckedDocumentAsync("no.perm.user");

        var (controller, _, _) = BuildController(new FakeCurrentUserService { UserId = 999, Roles = new List<string> { SystemRoles.DisciplineEngineer } });

        var result = await controller.Approve(doc.DocumentId, "trying to approve");

        Assert.IsType<ForbidResult>(result);
        var reloaded = await Ctx.EngineeringDocuments.AsNoTracking().FirstAsync(x => x.DocumentId == doc.DocumentId);
        Assert.Equal("Checked", reloaded.Status);
    }

    [Fact]
    public async Task Approve_SameUserWhoCreatedDocument_IsDeniedBySoD_DocumentRemainsUnapproved()
    {
        await SeedApprovePermissionAndSoDPolicyAsync();
        var (doc, user) = await CreateCheckedDocumentAsync("self.approver.wf");

        var accessControl = new AccessControlService(Ctx);

        // شبیه‌سازی این‌که همین کاربر مدرک را ایجاد کرده (دقیقاً همان چیزی که
        // EngineeringDocumentsController.Create حالا بعد از هر Create واقعی ثبت می‌کند)
        await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "EngineeringDocuments", ActionName = "Create", ResourceId = doc.DocumentId.ToString()
        });

        var (controller, _, _) = BuildController(new FakeCurrentUserService { UserId = user.Id, Roles = new List<string> { SystemRoles.EngineeringManager } });

        await controller.Approve(doc.DocumentId, "self approve attempt");

        // مدرک نباید تایید شده باشد - همچنان Checked است، نه Approved
        var reloaded = await Ctx.EngineeringDocuments.AsNoTracking().FirstAsync(x => x.DocumentId == doc.DocumentId);
        Assert.Equal("Checked", reloaded.Status);
    }

    [Fact]
    public async Task Approve_DifferentAuthorizedUser_Succeeds()
    {
        await SeedApprovePermissionAndSoDPolicyAsync();
        var (doc, creator) = await CreateCheckedDocumentAsync("creator.wf");

        var accessControl = new AccessControlService(Ctx);
        await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = creator.Id, ResourceName = "EngineeringDocuments", ActionName = "Create", ResourceId = doc.DocumentId.ToString()
        });

        var approverRole = Ctx.Roles.First(r => r.Name == SystemRoles.EngineeringManager);
        var approver = new PEMS.Persistence.Identity.ApplicationUser { UserName = "approver.wf", Email = "approver.wf@pems.local", FullName = "approver.wf" };
        Ctx.Users.Add(approver);
        await Ctx.SaveChangesAsync();
        Ctx.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<int> { UserId = approver.Id, RoleId = approverRole.Id });
        await Ctx.SaveChangesAsync();

        var (controller, _, _) = BuildController(new FakeCurrentUserService { UserId = approver.Id, Roles = new List<string> { SystemRoles.EngineeringManager } });

        await controller.Approve(doc.DocumentId, "legitimate approval");

        var reloaded = await Ctx.EngineeringDocuments.AsNoTracking().FirstAsync(x => x.DocumentId == doc.DocumentId);
        Assert.Equal("Approved", reloaded.Status);
    }
}
