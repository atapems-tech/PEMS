using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS — Final Global Repository Audit» بند ۱۹ (P0): ProcurementPackageController هیچ
/// بررسی مجوزی نداشت - هر کاربر احرازهویت‌شده‌ای (صرف‌نظر از نقش) می‌توانست Package بسازد،
/// مرحله را جلو ببرد، و از همه مهم‌تر AddApproval (تاییدیه‌ی خرید) را ثبت کند. این تست‌ها هم
/// مسیر مجوز و هم رفع IDOR روی RespondCommunication/SetCommunicationStatus (که قبلاً فقط با
/// communicationId پیدا می‌شدند، بدون تأیید تعلق به packageId درخواستی) را می‌آزمایند.
/// </summary>
public class ProcurementPackageControllerAuthorizationTests
{
    private static async Task<(ProcurementPackageController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(
        string dbName, string? role = SystemRoles.ContractManager)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"ProcPkgAuth_{dbName}");
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();

        var accessControl = new AccessControlService(ctx);
        var permissionService = new PermissionService(ctx, accessControl);
        await permissionService.SeedDefaultsAsync();

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        var provider = services.BuildServiceProvider();
        await AccessControlSeeder.SeedFromLegacyAsync(provider);
        // طبق ممیزی Package Procurement Contract Finalization (§F): بدون این، هیچ ردیف
        // SoDPolicy ای وجود ندارد و تست‌های خودتاییدی زیر چیزی برای بررسی پیدا نمی‌کنند
        await AccessControlSeeder.SeedPhase5GovernanceAsync(provider);

        // طبق AccessControlScopeAndSoDTests: AuthorizeAsync (مسیر SoD-آگاه) از FakeCurrentUserService.
        // Roles نمی‌خواند - نقش واقعی کاربر را از AspNetUserRoles/RoleAssignments در دیتابیس
        // می‌خواند. بدون این ردیف، هر بررسی SoD چون کاربر «هیچ نقشی ندارد» رد می‌شود، نه به‌خاطر SoD.
        var user = new ApplicationUser { UserName = $"user_{dbName}", Email = $"{dbName}@pems.local", FullName = "Test User" };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        if (role != null)
        {
            var roleEntity = ctx.Roles.First(r => r.Name == role);
            ctx.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<int> { UserId = user.Id, RoleId = roleEntity.Id });
            await ctx.SaveChangesAsync();
        }

        var currentUser = new FakeCurrentUserService
        {
            UserId = user.Id,
            Roles = role != null ? new List<string> { role } : new List<string>()
        };

        var service = new ProcurementPackageService(ctx);
        var controller = new ProcurementPackageController(service, ctx, currentUser, permissionService, accessControl).WithTempData();
        return (controller, ctx);
    }

    [Fact]
    public async Task Index_WithoutViewPermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Index_WithoutViewPermission_ReturnsForbid), role: null);

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_WithViewPermission_ReturnsView()
    {
        var (controller, _) = await CreateController(nameof(Index_WithViewPermission_ReturnsView), role: SystemRoles.Viewer);

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbid_DoesNotCreatePackage()
    {
        var (controller, ctx) = await CreateController(nameof(Create_WithoutCreatePermission_ReturnsForbid_DoesNotCreatePackage), role: SystemRoles.Viewer);
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "P1", ProjectName = "Project One", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.Create("PP-001", 1, null, "Title", null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.ProcurementPackages);
    }

    [Fact]
    public async Task Create_WithCreatePermission_CreatesPackage()
    {
        var (controller, ctx) = await CreateController(nameof(Create_WithCreatePermission_CreatesPackage), role: SystemRoles.DisciplineEngineer);
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "P1", ProjectName = "Project One", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.Create("PP-001", 1, null, "Title", null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(ctx.ProcurementPackages);
    }

    [Fact]
    public async Task AddApproval_WithoutApprovePermission_ReturnsForbid_DoesNotRecordApproval()
    {
        var (controller, ctx) = await CreateController(nameof(AddApproval_WithoutApprovePermission_ReturnsForbid_DoesNotRecordApproval), role: SystemRoles.DisciplineEngineer);
        ctx.ProcurementPackages.Add(new ProcurementPackage { ProcurementPackageId = 1, PackageNumber = "PP-001", ProjectId = 1, Title = "T", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.AddApproval(1, "Approved", null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.ProcurementApprovals);
    }

    [Fact]
    public async Task AddApproval_WithApprovePermission_RecordsApproval()
    {
        var (controller, ctx) = await CreateController(nameof(AddApproval_WithApprovePermission_RecordsApproval), role: SystemRoles.ContractManager);
        ctx.ProcurementPackages.Add(new ProcurementPackage { ProcurementPackageId = 1, PackageNumber = "PP-001", ProjectId = 1, Title = "T", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.AddApproval(1, "Approved", null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(ctx.ProcurementApprovals);
    }

    [Fact]
    public async Task Closeout_WithoutApprovePermission_ReturnsForbid()
    {
        var (controller, ctx) = await CreateController(nameof(Closeout_WithoutApprovePermission_ReturnsForbid), role: SystemRoles.DisciplineEngineer);
        ctx.ProcurementPackages.Add(new ProcurementPackage { ProcurementPackageId = 1, PackageNumber = "PP-001", ProjectId = 1, Title = "T", IsActive = true, Status = "Open" });
        await ctx.SaveChangesAsync();

        var result = await controller.Closeout(1);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal("Open", ctx.ProcurementPackages.Single().Status);
    }

    // ================= IDOR regression: RespondCommunication / SetCommunicationStatus =================

    [Fact]
    public async Task RespondCommunication_MismatchedPackageId_DoesNotModifyCommunication()
    {
        var (controller, ctx) = await CreateController(nameof(RespondCommunication_MismatchedPackageId_DoesNotModifyCommunication));
        ctx.ProcurementPackages.Add(new ProcurementPackage { ProcurementPackageId = 1, PackageNumber = "PP-A", ProjectId = 1, Title = "A", IsActive = true });
        ctx.ProcurementPackages.Add(new ProcurementPackage { ProcurementPackageId = 2, PackageNumber = "PP-B", ProjectId = 1, Title = "B", IsActive = true });
        ctx.ProcurementCommunications.Add(new ProcurementCommunication
        {
            ProcurementCommunicationId = 10,
            ProcurementPackageId = 1,
            Type = "Vendor Inquiry",
            Subject = "S",
            Body = "B",
            ActionStatus = "Open"
        });
        await ctx.SaveChangesAsync();

        // مهاجم packageId=2 (که به آن Communication ندارد) را همراه communicationId=10 (که واقعاً
        // متعلق به Package #1 است) می‌فرستد - نباید هیچ تغییری اعمال شود
        var result = await controller.RespondCommunication(2, 10, "attacker response");

        Assert.IsType<RedirectToActionResult>(result);
        var communication = ctx.ProcurementCommunications.Single(x => x.ProcurementCommunicationId == 10);
        Assert.Null(communication.ResponseText);
        Assert.Equal("Open", communication.ActionStatus);
    }

    [Fact]
    public async Task RespondCommunication_MatchingPackageId_UpdatesCommunication()
    {
        var (controller, ctx) = await CreateController(nameof(RespondCommunication_MatchingPackageId_UpdatesCommunication));
        ctx.ProcurementPackages.Add(new ProcurementPackage { ProcurementPackageId = 1, PackageNumber = "PP-A", ProjectId = 1, Title = "A", IsActive = true });
        ctx.ProcurementCommunications.Add(new ProcurementCommunication
        {
            ProcurementCommunicationId = 10,
            ProcurementPackageId = 1,
            Type = "Vendor Inquiry",
            Subject = "S",
            Body = "B",
            ActionStatus = "Open"
        });
        await ctx.SaveChangesAsync();

        var result = await controller.RespondCommunication(1, 10, "real response");

        Assert.IsType<RedirectToActionResult>(result);
        var communication = ctx.ProcurementCommunications.Single(x => x.ProcurementCommunicationId == 10);
        Assert.Equal("real response", communication.ResponseText);
        Assert.Equal("Answered", communication.ActionStatus);
    }

    [Fact]
    public async Task SetCommunicationStatus_MismatchedPackageId_DoesNotModifyCommunication()
    {
        var (controller, ctx) = await CreateController(nameof(SetCommunicationStatus_MismatchedPackageId_DoesNotModifyCommunication));
        ctx.ProcurementPackages.Add(new ProcurementPackage { ProcurementPackageId = 1, PackageNumber = "PP-A", ProjectId = 1, Title = "A", IsActive = true });
        ctx.ProcurementPackages.Add(new ProcurementPackage { ProcurementPackageId = 2, PackageNumber = "PP-B", ProjectId = 1, Title = "B", IsActive = true });
        ctx.ProcurementCommunications.Add(new ProcurementCommunication
        {
            ProcurementCommunicationId = 10,
            ProcurementPackageId = 1,
            Type = "Vendor Inquiry",
            Subject = "S",
            Body = "B",
            ActionStatus = "Open"
        });
        await ctx.SaveChangesAsync();

        var result = await controller.SetCommunicationStatus(2, 10, "Closed");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Open", ctx.ProcurementCommunications.Single(x => x.ProcurementCommunicationId == 10).ActionStatus);
    }

    // ================= Package Procurement Contract Finalization §F: SoD self-approval =================

    [Fact]
    public async Task AddApproval_BySameUserWhoCreatedThePackage_IsDeniedBySoD_DoesNotRecordApproval()
    {
        // ContractManager دارد هم Create و هم Approve را روی ProcurementPackage - این دقیقاً
        // همان شکاف زنده‌ای است که ممیزی §F پیدا کرد
        var (controller, ctx) = await CreateController(
            nameof(AddApproval_BySameUserWhoCreatedThePackage_IsDeniedBySoD_DoesNotRecordApproval), role: SystemRoles.ContractManager);
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "P1", ProjectName = "Project One", IsActive = true });
        await ctx.SaveChangesAsync();

        var createResult = await controller.Create("PP-001", 1, null, "Title", null);
        var packageId = ctx.ProcurementPackages.Single().ProcurementPackageId;
        Assert.IsType<RedirectToActionResult>(createResult);

        var approveResult = await controller.AddApproval(packageId, "Approved", null);

        Assert.IsType<RedirectToActionResult>(approveResult);
        Assert.Empty(ctx.ProcurementApprovals);
    }

    [Fact]
    public async Task AddApproval_ByDifferentUserThanCreator_Succeeds()
    {
        var (controller, ctx) = await CreateController(
            nameof(AddApproval_ByDifferentUserThanCreator_Succeeds), role: SystemRoles.ContractManager);
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "P1", ProjectName = "Project One", IsActive = true });
        await ctx.SaveChangesAsync();

        await controller.Create("PP-001", 1, null, "Title", null);
        var packageId = ctx.ProcurementPackages.Single().ProcurementPackageId;

        // شبیه‌سازی کاربر دوم (EngineeringManager) که خودش این Package را نساخته - باید یک
        // ApplicationUser/UserRoles واقعی در دیتابیس داشته باشد تا AuthorizeAsync (SoD-آگاه) او
        // را به‌عنوان نقش واقعی بشناسد (فقط FakeCurrentUserService.Roles برای این مسیر کافی نیست)
        var otherUser = new ApplicationUser { UserName = "second_user", Email = "second_user@pems.local", FullName = "Second User" };
        ctx.Users.Add(otherUser);
        await ctx.SaveChangesAsync();
        var engManagerRole = ctx.Roles.First(r => r.Name == SystemRoles.EngineeringManager);
        ctx.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<int> { UserId = otherUser.Id, RoleId = engManagerRole.Id });
        await ctx.SaveChangesAsync();

        var otherController = new ProcurementPackageController(
            new ProcurementPackageService(ctx), ctx,
            new FakeCurrentUserService { UserId = otherUser.Id, Roles = new List<string> { SystemRoles.EngineeringManager } },
            new PermissionService(ctx, new AccessControlService(ctx)),
            new AccessControlService(ctx)).WithTempData();

        var approveResult = await otherController.AddApproval(packageId, "Approved", null);

        Assert.IsType<RedirectToActionResult>(approveResult);
        Assert.Single(ctx.ProcurementApprovals);
    }

    // ================= Final Hardening Gap #2: Commercial/Contractual Clarification =================

    [Theory]
    [InlineData("TechnicalClarification")]
    [InlineData("CommercialClarification")]
    [InlineData("ContractualClarification")]
    [InlineData("VendorInquiry")]
    public async Task AddCommunication_SupportsAllClarificationTypes(string type)
    {
        var (controller, ctx) = await CreateController(nameof(AddCommunication_SupportsAllClarificationTypes) + type, role: SystemRoles.ContractManager);
        ctx.ProcurementPackages.Add(new ProcurementPackage { ProcurementPackageId = 1, PackageNumber = "PP-A", ProjectId = 1, Title = "A", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.AddCommunication(1, type, "Subject", "Body", null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(type, ctx.ProcurementCommunications.Single().Type);
    }

    [Fact]
    public async Task RespondCommunication_TwiceInARow_PreservesBothResponsesInHistory_NotJustTheLatest()
    {
        var (controller, ctx) = await CreateController(nameof(RespondCommunication_TwiceInARow_PreservesBothResponsesInHistory_NotJustTheLatest));
        ctx.ProcurementPackages.Add(new ProcurementPackage { ProcurementPackageId = 1, PackageNumber = "PP-A", ProjectId = 1, Title = "A", IsActive = true });
        ctx.ProcurementCommunications.Add(new ProcurementCommunication
        {
            ProcurementCommunicationId = 10,
            ProcurementPackageId = 1,
            Type = "CommercialClarification",
            Subject = "S",
            Body = "B",
            ActionStatus = "Open"
        });
        await ctx.SaveChangesAsync();

        await controller.RespondCommunication(1, 10, "first response");
        await controller.RespondCommunication(1, 10, "corrected response");

        // طبق گزارش سخت‌افزاری نهایی (Gap ۲): پاسخ اول نباید بدون اثر گم شده باشد
        var history = ctx.ProcurementCommunicationResponses
            .Where(x => x.ProcurementCommunicationId == 10)
            .OrderBy(x => x.ProcurementCommunicationResponseId)
            .ToList();

        Assert.Equal(2, history.Count);
        Assert.Equal("first response", history[0].ResponseText);
        Assert.Equal("corrected response", history[1].ResponseText);

        // Cache روی خودِ رکورد باید آخرین پاسخ را نشان دهد
        Assert.Equal("corrected response", ctx.ProcurementCommunications.Single().ResponseText);
    }

    [Fact]
    public async Task ProcurementCommunication_Create_IsAuditLogged()
    {
        // طبق گزارش سخت‌افزاری نهایی (Gap ۲): ProcurementCommunication اکنون BaseEntity را
        // ارث می‌برد - تغییرات آن باید مثل بقیه‌ی Entity ها Audit شوند
        var (controller, ctx) = await CreateController(nameof(ProcurementCommunication_Create_IsAuditLogged), role: SystemRoles.ContractManager);
        ctx.ProcurementPackages.Add(new ProcurementPackage { ProcurementPackageId = 1, PackageNumber = "PP-A", ProjectId = 1, Title = "A", IsActive = true });
        await ctx.SaveChangesAsync();

        await controller.AddCommunication(1, "CommercialClarification", "Subject", "Body", null);

        var auditRow = ctx.AuditLogs.FirstOrDefault(x => x.EntityName == nameof(ProcurementCommunication));
        Assert.NotNull(auditRow);
    }
}
