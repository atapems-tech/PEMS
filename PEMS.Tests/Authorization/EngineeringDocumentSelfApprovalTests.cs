using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS EDMS Phase 4 Audit» یافته P4-02: AccessControlService.AuthorizeAsync و
/// SoDPolicy («EngineeringDocuments.Create/Edit در تعارض با Approve») از فاز ۱ RBAC-ABAC ساخته
/// و تست شده بودند (AccessControlScopeAndSoDTests با یک مثال دستی روی "MaterialRequisition")
/// اما هیچ‌جای پروژه واقعاً فراخوانی نمی‌شدند - این تست‌ها دقیقاً همان سیاست واقعیِ Seed‌شده‌ی
/// AccessControlSeeder.SeedPhase5GovernanceAsync را برای ماژول واقعی "EngineeringDocuments"
/// اجرا می‌کنند (نه یک معادل دستی)، تا ثابت شود سیاست تولید واقعی درست کار می‌کند - نه فقط
/// این‌که مکانیزم عمومی درست است.
/// </summary>
public class EngineeringDocumentSelfApprovalTests
{
    private static async Task<(PEMS.Persistence.Context.PEMSDbContext ctx, AccessControlService accessControl)> SeedAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory(dbName);

        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        var provider = services.BuildServiceProvider();

        // همان سیاست واقعی تولید - نه یک نسخه‌ی دستی ساخته‌شده برای تست
        await AccessControlSeeder.SeedPhase5GovernanceAsync(provider);

        return (ctx, new AccessControlService(ctx));
    }

    private static async Task GrantAsync(PEMS.Persistence.Context.PEMSDbContext ctx, string roleName, string resourceName, string actionName)
    {
        var role = ctx.Roles.First(r => r.Name == roleName);

        var permission = await ctx.Permissions.FirstOrDefaultAsync(p => p.ResourceName == resourceName && p.ActionName == actionName);
        if (permission == null)
        {
            permission = new Permission { Name = $"{resourceName}.{actionName}", ResourceName = resourceName, ActionName = actionName, IsActive = true };
            ctx.Permissions.Add(permission);
            await ctx.SaveChangesAsync();
        }

        ctx.RolePermissionGrants.Add(new RolePermissionGrant { RoleId = role.Id, PermissionId = permission.PermissionId, IsActive = true });
        await ctx.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> CreateUserInRoleAsync(PEMS.Persistence.Context.PEMSDbContext ctx, string username, string roleName)
    {
        var user = new ApplicationUser { UserName = username, Email = $"{username}@pems.local", FullName = username };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var role = ctx.Roles.First(r => r.Name == roleName);
        ctx.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<int> { UserId = user.Id, RoleId = role.Id });
        await ctx.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task RealSeededPolicy_UserWhoCreatedDocument_CannotApproveSameDocument()
    {
        var (ctx, accessControl) = await SeedAsync(nameof(RealSeededPolicy_UserWhoCreatedDocument_CannotApproveSameDocument));
        await GrantAsync(ctx, SystemRoles.EngineeringManager, "EngineeringDocuments", "Create");
        await GrantAsync(ctx, SystemRoles.EngineeringManager, "EngineeringDocuments", "Approve");
        var user = await CreateUserInRoleAsync(ctx, "self.approver", SystemRoles.EngineeringManager);

        var createDecision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "EngineeringDocuments", ActionName = "Create", ResourceId = "42"
        });
        Assert.True(createDecision.Allowed);

        var approveDecision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "EngineeringDocuments", ActionName = "Approve", ResourceId = "42"
        });

        Assert.False(approveDecision.Allowed);
        Assert.Equal(AccessDecisionReason.DeniedBySoD, approveDecision.Reason);
    }

    [Fact]
    public async Task RealSeededPolicy_UserWhoEditedDocument_CannotApproveSameDocument()
    {
        var (ctx, accessControl) = await SeedAsync(nameof(RealSeededPolicy_UserWhoEditedDocument_CannotApproveSameDocument));
        await GrantAsync(ctx, SystemRoles.EngineeringManager, "EngineeringDocuments", "Edit");
        await GrantAsync(ctx, SystemRoles.EngineeringManager, "EngineeringDocuments", "Approve");
        var user = await CreateUserInRoleAsync(ctx, "self.editor", SystemRoles.EngineeringManager);

        await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "EngineeringDocuments", ActionName = "Edit", ResourceId = "77"
        });

        var approveDecision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "EngineeringDocuments", ActionName = "Approve", ResourceId = "77"
        });

        Assert.False(approveDecision.Allowed);
        Assert.Equal(AccessDecisionReason.DeniedBySoD, approveDecision.Reason);
    }

    [Fact]
    public async Task RealSeededPolicy_DifferentUserApproves_Allowed()
    {
        var (ctx, accessControl) = await SeedAsync(nameof(RealSeededPolicy_DifferentUserApproves_Allowed));
        await GrantAsync(ctx, SystemRoles.EngineeringManager, "EngineeringDocuments", "Create");
        await GrantAsync(ctx, SystemRoles.EngineeringManager, "EngineeringDocuments", "Approve");
        var creator = await CreateUserInRoleAsync(ctx, "creator.user", SystemRoles.EngineeringManager);
        var approver = await CreateUserInRoleAsync(ctx, "approver.user", SystemRoles.EngineeringManager);

        await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = creator.Id, ResourceName = "EngineeringDocuments", ActionName = "Create", ResourceId = "99"
        });

        var approveDecision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = approver.Id, ResourceName = "EngineeringDocuments", ActionName = "Approve", ResourceId = "99"
        });

        Assert.True(approveDecision.Allowed);
    }

    [Fact]
    public async Task RealSeededPolicy_SameUserApprovesDifferentDocument_Allowed()
    {
        var (ctx, accessControl) = await SeedAsync(nameof(RealSeededPolicy_SameUserApprovesDifferentDocument_Allowed));
        await GrantAsync(ctx, SystemRoles.EngineeringManager, "EngineeringDocuments", "Create");
        await GrantAsync(ctx, SystemRoles.EngineeringManager, "EngineeringDocuments", "Approve");
        var user = await CreateUserInRoleAsync(ctx, "multi.doc.user", SystemRoles.EngineeringManager);

        await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "EngineeringDocuments", ActionName = "Create", ResourceId = "100"
        });

        // مدرک ۱۰۱ کاملاً متفاوت است - نباید تحت‌تاثیر SoD روی مدرک ۱۰۰ قرار بگیرد
        var approveDecision = await accessControl.AuthorizeAsync(new AccessControlRequest
        {
            UserId = user.Id, ResourceName = "EngineeringDocuments", ActionName = "Approve", ResourceId = "101"
        });

        Assert.True(approveDecision.Allowed);
    }
}
