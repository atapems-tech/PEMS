using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;
using PEMS.Web.Security;

namespace PEMS.Tests.Authorization;

/// <summary>
/// Phase 1 security hardening: Closeout / AddApproval / PO Issue باید Fail-Closed باشند - فقط Allowed صریحِ مسیر
/// آگاه-از-کاربر عبور می‌دهد؛ DeniedByScope، نبود شناسه‌ی کاربر، نتیجه‌ی غیرمنتظره و خطای ارزیابی همگی رد هستند، هیچ
/// تغییر وضعیتی رخ نمی‌دهد، و هر تلاش ردشده دقیقاً یک ردیف AuthorizationAuditLog دارد.
/// </summary>
public class ProtectedActionGuardTests
{
    private static async Task<PEMSDbContext> DbAsync(string name)
    {
        var ctx = PemsDbContextFactory.CreateInMemory("Guard_" + name);
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();
        var access = new AccessControlService(ctx);
        await new PermissionService(ctx, access).SeedDefaultsAsync();
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        var provider = services.BuildServiceProvider();
        await AccessControlSeeder.SeedFromLegacyAsync(provider);
        await AccessControlSeeder.SeedPhase5GovernanceAsync(provider);
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "P1", ProjectName = "P", IsActive = true });
        ctx.Vendors.Add(new Vendor { VendorId = 1, Name = "V", IsActive = true });
        ctx.ProcurementPackages.Add(new ProcurementPackage { ProcurementPackageId = 1, PackageNumber = "PP-1", ProjectId = 1, Title = "T", IsActive = true });
        await ctx.SaveChangesAsync();
        return ctx;
    }

    private static async Task<ApplicationUser> UserAsync(PEMSDbContext ctx, string name, string? directRole)
    {
        var u = new ApplicationUser { UserName = name, Email = name + "@x.local", FullName = name };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        if (directRole != null)
        {
            ctx.UserRoles.Add(new IdentityUserRole<int> { UserId = u.Id, RoleId = ctx.Roles.First(r => r.Name == directRole).Id });
            await ctx.SaveChangesAsync();
        }
        return u;
    }

    /// <summary>کاربری که نقش را فقط با RoleAssignment محدود به پروژه‌ای دیگر دارد (نه سراسری) ⇒ AuthorizeAsync = DeniedByScope.</summary>
    private static async Task<ApplicationUser> ScopedOnlyUserAsync(PEMSDbContext ctx, string name, string role)
    {
        var u = await UserAsync(ctx, name, null);
        ctx.RoleAssignments.Add(new RoleAssignment
        {
            UserId = u.Id, RoleId = ctx.Roles.First(r => r.Name == role).Id,
            ScopeType = "Project", ScopeValue = "SOME-OTHER-PROJECT", IsActive = true
        });
        await ctx.SaveChangesAsync();
        return u;
    }

    private static ProcurementPackageController PackageController(PEMSDbContext ctx, int? userId, string role, IAccessControlService? accessOverride = null)
    {
        var access = new AccessControlService(ctx);
        return new ProcurementPackageController(new ProcurementPackageService(ctx), ctx,
            new FakeCurrentUserService { UserId = userId, Roles = new List<string> { role } },
            new PermissionService(ctx, access), accessOverride ?? access).WithTempData();
    }

    private static PurchaseOrderController PoController(PEMSDbContext ctx, int? userId, string role, IAccessControlService? accessOverride = null)
    {
        var access = new AccessControlService(ctx);
        return new PurchaseOrderController(new PurchaseOrderService(ctx), new PermissionService(ctx, access),
            new FakeCurrentUserService { UserId = userId, Roles = new List<string> { role } }, ctx, accessOverride ?? access).WithTempData();
    }

    private static async Task GrantAsync(PEMSDbContext ctx, string role, string resource, string action)
    {
        var p = await ctx.Permissions.FirstOrDefaultAsync(x => x.ResourceName == resource && x.ActionName == action);
        if (p == null)
        {
            p = new Permission { Name = $"{resource}.{action}", ResourceName = resource, ActionName = action, IsActive = true };
            ctx.Permissions.Add(p);
            await ctx.SaveChangesAsync();
        }
        ctx.RolePermissionGrants.Add(new RolePermissionGrant { RoleId = ctx.Roles.First(r => r.Name == role).Id, PermissionId = p.PermissionId, IsActive = true });
        await ctx.SaveChangesAsync();
    }

    private static string PackageState(PEMSDbContext ctx) { var p = ctx.ProcurementPackages.Single(); return $"{p.Status}/{p.Stage}/{p.ClosedDate}"; }

    // ───────────────────────── DeniedByScope ─────────────────────────

    [Fact]
    public async Task Closeout_DeniedByScope_ReturnsForbid_PackageStaysOpen_AndDenialIsAuditedOnce()
    {
        var ctx = await DbAsync(nameof(Closeout_DeniedByScope_ReturnsForbid_PackageStaysOpen_AndDenialIsAuditedOnce));
        var u = await ScopedOnlyUserAsync(ctx, "scoped", SystemRoles.EngineeringManager);
        var before = PackageState(ctx);

        var result = await PackageController(ctx, u.Id, SystemRoles.EngineeringManager).Closeout(1);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(before, PackageState(ctx));
        var rows = ctx.AuthorizationAuditLogs.Where(a => a.UserId == u.Id && a.ResourceName == "ProcurementPackage" && a.ActionName == "Approve").ToList();
        Assert.Single(rows);
        Assert.Equal(nameof(AccessDecisionReason.DeniedByScope), rows[0].Decision);
    }

    [Fact]
    public async Task AddApproval_DeniedByScope_ReturnsForbid_AndRecordsNoApproval()
    {
        var ctx = await DbAsync(nameof(AddApproval_DeniedByScope_ReturnsForbid_AndRecordsNoApproval));
        var u = await ScopedOnlyUserAsync(ctx, "scoped", SystemRoles.EngineeringManager);
        var before = PackageState(ctx);

        var result = await PackageController(ctx, u.Id, SystemRoles.EngineeringManager).AddApproval(1, "Approved", null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.ProcurementApprovals);
        Assert.Equal(before, PackageState(ctx));
    }

    [Fact]
    public async Task PoIssue_DeniedByScope_ReturnsForbid_AndPoStaysDraft()
    {
        var ctx = await DbAsync(nameof(PoIssue_DeniedByScope_ReturnsForbid_AndPoStaysDraft));
        await GrantAsync(ctx, SystemRoles.EngineeringManager, "PurchaseOrder", "Approve");
        var creator = await UserAsync(ctx, "creator", SystemRoles.ContractManager);
        await PoController(ctx, creator.Id, SystemRoles.ContractManager).Create("PO-1", 1, 1, null, null, 1m);
        var u = await ScopedOnlyUserAsync(ctx, "scoped", SystemRoles.EngineeringManager);

        var result = await PoController(ctx, u.Id, SystemRoles.EngineeringManager).Issue(ctx.PurchaseOrders.Single().PoId);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal("Draft", ctx.PurchaseOrders.Single().Status);
    }

    // ───────────────────────── Missing identity ─────────────────────────

    [Fact]
    public async Task Closeout_AddApproval_Issue_WithoutUserId_AreDenied_NoMutation_AndAudited()
    {
        var ctx = await DbAsync(nameof(Closeout_AddApproval_Issue_WithoutUserId_AreDenied_NoMutation_AndAudited));
        var creator = await UserAsync(ctx, "creator", SystemRoles.ContractManager);
        await PoController(ctx, creator.Id, SystemRoles.ContractManager).Create("PO-1", 1, 1, null, null, 1m);
        await GrantAsync(ctx, SystemRoles.ContractManager, "PurchaseOrder", "Approve");
        var before = PackageState(ctx);

        Assert.IsType<ForbidResult>(await PackageController(ctx, null, SystemRoles.ContractManager).Closeout(1));
        Assert.IsType<ForbidResult>(await PackageController(ctx, null, SystemRoles.ContractManager).AddApproval(1, "Approved", null));
        Assert.IsType<ForbidResult>(await PoController(ctx, null, SystemRoles.ContractManager).Issue(ctx.PurchaseOrders.Single().PoId));

        Assert.Equal(before, PackageState(ctx));
        Assert.Empty(ctx.ProcurementApprovals);
        Assert.Equal("Draft", ctx.PurchaseOrders.Single().Status);
        var denials = ctx.AuthorizationAuditLogs.Where(a => a.UserId == null && a.Decision == nameof(AccessDecisionReason.DeniedByPolicy)).ToList();
        Assert.Equal(3, denials.Count);
    }

    // ───────────────────────── Existing behaviours preserved ─────────────────────────

    [Fact]
    public async Task SoDDenial_StillRedirectsWithError_AndMutatesNothing_OnCloseoutAndAddApproval()
    {
        var ctx = await DbAsync(nameof(SoDDenial_StillRedirectsWithError_AndMutatesNothing_OnCloseoutAndAddApproval));
        var creator = await UserAsync(ctx, "creator", SystemRoles.ContractManager);
        var c = PackageController(ctx, creator.Id, SystemRoles.ContractManager);
        ctx.ProcurementPackages.RemoveRange(ctx.ProcurementPackages);
        await ctx.SaveChangesAsync();
        await c.Create("PP-NEW", 1, null, "T", null);
        var id = ctx.ProcurementPackages.Single().ProcurementPackageId;
        var before = PackageState(ctx);

        var closeout = await c.Closeout(id);
        var approval = await c.AddApproval(id, "Approved", null);

        Assert.IsType<RedirectToActionResult>(closeout);
        Assert.IsType<RedirectToActionResult>(approval);
        Assert.NotNull(c.TempData["Error"]);
        Assert.Equal(before, PackageState(ctx));
        Assert.Empty(ctx.ProcurementApprovals);
        Assert.Contains(ctx.AuthorizationAuditLogs, a => a.UserId == creator.Id && a.Decision == nameof(AccessDecisionReason.DeniedBySoD));
    }

    [Fact]
    public async Task IndependentApprover_StillApprovesAndCloses()
    {
        var ctx = await DbAsync(nameof(IndependentApprover_StillApprovesAndCloses));
        var approver = await UserAsync(ctx, "approver", SystemRoles.EngineeringManager);
        var c = PackageController(ctx, approver.Id, SystemRoles.EngineeringManager);

        Assert.IsType<RedirectToActionResult>(await c.AddApproval(1, "Approved", null));
        Assert.Single(ctx.ProcurementApprovals);
        Assert.IsType<RedirectToActionResult>(await c.Closeout(1));
        Assert.Equal("Closed", ctx.ProcurementPackages.Single().Status);
        Assert.Null(c.TempData["Error"]);
    }

    // ───────────────────────── Role-only denial auditing ─────────────────────────

    [Fact]
    public async Task RoleOnlyDenials_AreAudited_ExactlyOncePerAttempt_WithDiagnostics()
    {
        var ctx = await DbAsync(nameof(RoleOnlyDenials_AreAudited_ExactlyOncePerAttempt_WithDiagnostics));
        var viewer = await UserAsync(ctx, "viewer", SystemRoles.Viewer);
        var admin = await UserAsync(ctx, "admin", SystemRoles.SystemAdmin);
        var creator = await UserAsync(ctx, "creator", SystemRoles.ContractManager);
        await PoController(ctx, creator.Id, SystemRoles.ContractManager).Create("PO-1", 1, 1, null, null, 1m);
        var poId = ctx.PurchaseOrders.Single().PoId;
        var before = ctx.AuthorizationAuditLogs.Count();

        Assert.IsType<ForbidResult>(await PackageController(ctx, viewer.Id, SystemRoles.Viewer).Closeout(1));
        Assert.IsType<ForbidResult>(await PackageController(ctx, viewer.Id, SystemRoles.Viewer).AddApproval(1, "Approved", null));
        Assert.IsType<ForbidResult>(await PoController(ctx, admin.Id, SystemRoles.SystemAdmin).Issue(poId));   // SystemAdmin بدون Grant صریح

        var added = ctx.AuthorizationAuditLogs.OrderBy(a => a.AuthorizationAuditLogId).Skip(before).ToList();
        Assert.Equal(3, added.Count);
        Assert.All(added, a => Assert.Equal(nameof(AccessDecisionReason.DeniedByRole), a.Decision));
        Assert.All(added, a => Assert.False(string.IsNullOrWhiteSpace(a.Reason)));
        Assert.Contains(added, a => a.UserId == admin.Id && a.ResourceName == "PurchaseOrder" && a.ResourceId == poId.ToString());
        Assert.Equal("Draft", ctx.PurchaseOrders.Single().Status);
        Assert.Empty(ctx.ProcurementApprovals);
    }

    // ───────────────────────── Fail-closed on unexpected results / failures ─────────────────────────

    private sealed class ScriptedAccess : IAccessControlService
    {
        public Func<AccessDecision?>? Decision { get; init; }
        public bool ThrowOnAuthorize { get; init; }
        public bool ThrowOnRecord { get; init; }
        public int AuthorizeCalls, RecordCalls;
        public Task<AccessDecision> AuthorizeAsync(AccessControlRequest request)
        {
            AuthorizeCalls++;
            if (ThrowOnAuthorize) throw new InvalidOperationException("audit store unavailable");
            return Task.FromResult(Decision!()!);
        }
        public Task<AccessDecision> AuthorizeForRolesAsync(IEnumerable<string> roles, string resourceName, string actionName)
            => Task.FromResult(AccessDecision.Allow());
        public Task RecordDenialAsync(int? userId, string resourceName, string actionName, string? resourceId, string? ipAddress,
            AccessDecisionReason reason, string detail)
        {
            RecordCalls++;
            if (ThrowOnRecord) throw new InvalidOperationException("audit store unavailable");
            return Task.CompletedTask;
        }
    }

    public static IEnumerable<object[]> UnexpectedDecisions() => new[]
    {
        new object[] { "null decision", (Func<AccessDecision?>)(() => null) },
        new object[] { "Allowed flag with non-Allow reason", () => new AccessDecision { Allowed = true, Reason = AccessDecisionReason.DeniedByPolicy } },
        new object[] { "DeniedByRole", () => AccessDecision.Deny(AccessDecisionReason.DeniedByRole, "x") },
        new object[] { "DeniedByWorkflowState", () => AccessDecision.Deny(AccessDecisionReason.DeniedByWorkflowState, "x") },
        new object[] { "DeniedByOwnership", () => AccessDecision.Deny(AccessDecisionReason.DeniedByOwnership, "x") },
    };

    [Theory]
    [MemberData(nameof(UnexpectedDecisions))]
    public async Task UnexpectedOrNonAllowedDecisions_AreForbidden_AndNothingChanges(string label, Func<AccessDecision?> decision)
    {
        var ctx = await DbAsync(nameof(UnexpectedOrNonAllowedDecisions_AreForbidden_AndNothingChanges) + label.Replace(' ', '_'));
        var u = await UserAsync(ctx, "approver", SystemRoles.EngineeringManager);
        var access = new ScriptedAccess { Decision = decision };
        var before = PackageState(ctx);

        Assert.IsType<ForbidResult>(await PackageController(ctx, u.Id, SystemRoles.EngineeringManager, access).Closeout(1));
        Assert.IsType<ForbidResult>(await PackageController(ctx, u.Id, SystemRoles.EngineeringManager, access).AddApproval(1, "Approved", null));

        Assert.Equal(before, PackageState(ctx));
        Assert.Empty(ctx.ProcurementApprovals);
        Assert.Equal(2, access.AuthorizeCalls);
    }

    [Fact]
    public async Task EvaluationFailure_IsForbidden_NotAnUnhandledOrAllowedOutcome()
    {
        var ctx = await DbAsync(nameof(EvaluationFailure_IsForbidden_NotAnUnhandledOrAllowedOutcome));
        var u = await UserAsync(ctx, "approver", SystemRoles.EngineeringManager);
        var access = new ScriptedAccess { ThrowOnAuthorize = true };

        Assert.IsType<ForbidResult>(await PackageController(ctx, u.Id, SystemRoles.EngineeringManager, access).Closeout(1));

        Assert.NotEqual("Closed", ctx.ProcurementPackages.Single().Status);
    }

    [Fact]
    public async Task AuditFailureOnRoleOnlyDenial_StillDenies()
    {
        var ctx = await DbAsync(nameof(AuditFailureOnRoleOnlyDenial_StillDenies));
        var viewer = await UserAsync(ctx, "viewer", SystemRoles.Viewer);
        var access = new ScriptedAccess { ThrowOnRecord = true, Decision = () => AccessDecision.Allow() };

        var result = await PackageController(ctx, viewer.Id, SystemRoles.Viewer, access).Closeout(1);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(1, access.RecordCalls);
        Assert.Equal(0, access.AuthorizeCalls);
        Assert.NotEqual("Closed", ctx.ProcurementPackages.Single().Status);
    }

    [Fact]
    public async Task RecordDenialAsync_RefusesToRecordAnAllow()
    {
        var ctx = await DbAsync(nameof(RecordDenialAsync_RefusesToRecordAnAllow));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new AccessControlService(ctx).RecordDenialAsync(1, "X", "Y", null, null, AccessDecisionReason.Allow, "no"));
    }
}
