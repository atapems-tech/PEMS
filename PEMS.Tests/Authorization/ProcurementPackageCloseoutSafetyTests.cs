using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Authorization;

/// <summary>
/// Procurement Lifecycle - Work Package A: Closeout نباید از مسیر تغییر مرحله‌ی عمومی
/// (AdvanceStage) دور زده شود. تنها مسیر بستن Package، عملیات Closeout است که گیت بازرسی (سرویس)،
/// مجوز Approve و SoD (کنترلر) را رعایت می‌کند. این تست‌ها هم رفتار سرویس و هم مسیر کنترلر
/// را پوشش می‌دهند و سیاست‌های موجود (انتقال‌های مجاز مرحله) را حفظ می‌کنند.
/// </summary>
public class ProcurementPackageCloseoutSafetyTests
{
    // ───────────────────────── Service level ─────────────────────────

    private static async Task<(PEMSDbContext Ctx, ProcurementPackageService Service, int PackageId)> CreateServiceAsync(
        string dbName, bool requiresInspection = false, string status = "Open")
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"PkgCloseout_{dbName}");
        var project = new Project { ProjectCode = "P1", ProjectName = "Project One", IsActive = true };
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        var package = new ProcurementPackage
        {
            PackageNumber = "PP-001",
            ProjectId = project.ProjectId,
            Title = "Critical Pumps Package",
            IsActive = true,
            RequiresInspection = requiresInspection,
            Status = status
        };
        ctx.ProcurementPackages.Add(package);
        await ctx.SaveChangesAsync();

        return (ctx, new ProcurementPackageService(ctx), package.ProcurementPackageId);
    }

    [Fact]
    public async Task AdvanceStage_ToCloseout_IsRejected_AndPackageStaysOpen()
    {
        var (ctx, service, id) = await CreateServiceAsync(nameof(AdvanceStage_ToCloseout_IsRejected_AndPackageStaysOpen));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AdvanceStageAsync(id, "Closeout"));

        var package = ctx.ProcurementPackages.Single();
        Assert.Equal("Open", package.Status);
        Assert.NotEqual("Closeout", package.Stage);
        Assert.Null(package.ClosedDate);
    }

    [Fact]
    public async Task AdvanceStage_ToCloseout_DoesNotBypassTheInspectionGate()
    {
        // دقیقاً همان دور زدن اصلی: بسته‌ی نیازمند بازرسی بدون هیچ ITP
        var (ctx, service, id) = await CreateServiceAsync(
            nameof(AdvanceStage_ToCloseout_DoesNotBypassTheInspectionGate), requiresInspection: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AdvanceStageAsync(id, "Closeout"));

        var package = ctx.ProcurementPackages.Single();
        Assert.NotEqual("Closed", package.Status);
        Assert.Null(package.ClosedDate);
    }

    [Fact]
    public async Task AdvanceStage_UnknownStage_IsRejected()
    {
        var (ctx, service, id) = await CreateServiceAsync(nameof(AdvanceStage_UnknownStage_IsRejected));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AdvanceStageAsync(id, "Bogus"));

        Assert.Equal("Open", ctx.ProcurementPackages.Single().Status);
    }

    [Theory]
    [InlineData("MR")]
    [InlineData("VendorInquiry")]
    [InlineData("TechnicalClarification")]
    [InlineData("MRQ")]
    [InlineData("Response")]
    [InlineData("Approval")]
    public async Task AdvanceStage_SupportedStages_StillWork_AndMarkInProgress(string stage)
    {
        var (ctx, service, id) = await CreateServiceAsync(nameof(AdvanceStage_SupportedStages_StillWork_AndMarkInProgress) + stage);

        await service.AdvanceStageAsync(id, stage);

        var package = ctx.ProcurementPackages.Single();
        Assert.Equal(stage, package.Stage);
        Assert.Equal("InProgress", package.Status);
        Assert.Null(package.ClosedDate);
    }

    [Fact]
    public async Task AdvanceStage_BackwardsAmongOpenStages_StillWorks()
    {
        // انتقال به عقب (بازگشت برای اصلاح) همیشه مجاز بوده و حفظ می‌شود
        var (ctx, service, id) = await CreateServiceAsync(nameof(AdvanceStage_BackwardsAmongOpenStages_StillWorks));

        await service.AdvanceStageAsync(id, "Approval");
        await service.AdvanceStageAsync(id, "MRQ");

        Assert.Equal("MRQ", ctx.ProcurementPackages.Single().Stage);
    }

    [Theory]
    [InlineData("Closed")]
    [InlineData("Cancelled")]
    public async Task AdvanceStage_OnTerminalPackage_IsRejected_AndNothingChanges(string status)
    {
        var (ctx, service, id) = await CreateServiceAsync(nameof(AdvanceStage_OnTerminalPackage_IsRejected_AndNothingChanges) + status, status: status);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AdvanceStageAsync(id, "MR"));

        Assert.Equal(status, ctx.ProcurementPackages.Single().Status);
    }

    [Fact]
    public async Task Closeout_ValidPackage_ClosesWithStageStatusAndDate()
    {
        var (ctx, service, id) = await CreateServiceAsync(nameof(Closeout_ValidPackage_ClosesWithStageStatusAndDate));

        await service.CloseoutAsync(id);

        var package = ctx.ProcurementPackages.Single();
        Assert.Equal("Closeout", package.Stage);
        Assert.Equal("Closed", package.Status);
        Assert.NotNull(package.ClosedDate);
    }

    [Fact]
    public async Task Closeout_RequiresInspection_WithAllItpsClosed_Closes()
    {
        var (ctx, service, id) = await CreateServiceAsync(nameof(Closeout_RequiresInspection_WithAllItpsClosed_Closes), requiresInspection: true);
        ctx.InspectionTestPlans.Add(new InspectionTestPlan { ProcurementPackageId = id, Status = "Closed" });
        await ctx.SaveChangesAsync();

        await service.CloseoutAsync(id);

        Assert.Equal("Closed", ctx.ProcurementPackages.Single().Status);
    }

    [Theory]
    [InlineData("Open")]
    [InlineData("InProgress")]
    public async Task Closeout_RequiresInspection_WithIncompleteItp_IsBlocked_AndPackageNotClosed(string itpStatus)
    {
        var (ctx, service, id) = await CreateServiceAsync(
            nameof(Closeout_RequiresInspection_WithIncompleteItp_IsBlocked_AndPackageNotClosed) + itpStatus, requiresInspection: true);
        ctx.InspectionTestPlans.Add(new InspectionTestPlan { ProcurementPackageId = id, Status = itpStatus });
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseoutAsync(id));

        var package = ctx.ProcurementPackages.Single();
        Assert.NotEqual("Closed", package.Status);
        Assert.Null(package.ClosedDate);
    }

    [Fact]
    public async Task Closeout_RequiresInspection_WithNoItp_IsBlocked()
    {
        var (ctx, service, id) = await CreateServiceAsync(nameof(Closeout_RequiresInspection_WithNoItp_IsBlocked), requiresInspection: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseoutAsync(id));

        Assert.NotEqual("Closed", ctx.ProcurementPackages.Single().Status);
    }

    [Fact]
    public async Task Closeout_Twice_IsRejected_AndDoesNotRewriteClosedDate()
    {
        var (ctx, service, id) = await CreateServiceAsync(nameof(Closeout_Twice_IsRejected_AndDoesNotRewriteClosedDate));
        await service.CloseoutAsync(id);
        var firstDate = ctx.ProcurementPackages.Single().ClosedDate;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseoutAsync(id));

        Assert.Equal(firstDate, ctx.ProcurementPackages.Single().ClosedDate);
    }

    [Fact]
    public async Task Closeout_CancelledPackage_IsRejected()
    {
        var (ctx, service, id) = await CreateServiceAsync(nameof(Closeout_CancelledPackage_IsRejected), status: "Cancelled");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseoutAsync(id));

        Assert.Equal("Cancelled", ctx.ProcurementPackages.Single().Status);
    }

    // ───────────────────────── Controller level ─────────────────────────

    private static async Task<PEMSDbContext> CreateSeededDbAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"PkgCloseoutCtl_{dbName}");
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();

        var accessControl = new AccessControlService(ctx);
        await new PermissionService(ctx, accessControl).SeedDefaultsAsync();

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        var provider = services.BuildServiceProvider();
        await AccessControlSeeder.SeedFromLegacyAsync(provider);
        await AccessControlSeeder.SeedPhase5GovernanceAsync(provider);

        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "P1", ProjectName = "Project One", IsActive = true });
        await ctx.SaveChangesAsync();
        return ctx;
    }

    /// <summary>کاربر واقعی (با نقش واقعی در دیتابیس) می‌سازد تا مسیر SoD-آگاه او را بشناسد.</summary>
    private static async Task<ProcurementPackageController> ControllerForAsync(PEMSDbContext ctx, string userName, string role)
    {
        var user = new ApplicationUser { UserName = userName, Email = $"{userName}@pems.local", FullName = userName };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var roleEntity = ctx.Roles.First(r => r.Name == role);
        ctx.UserRoles.Add(new IdentityUserRole<int> { UserId = user.Id, RoleId = roleEntity.Id });
        await ctx.SaveChangesAsync();

        var accessControl = new AccessControlService(ctx);
        return new ProcurementPackageController(
            new ProcurementPackageService(ctx), ctx,
            new FakeCurrentUserService { UserId = user.Id, Roles = new List<string> { role }, FullName = userName },
            new PermissionService(ctx, accessControl), accessControl).WithTempData();
    }

    private static async Task<int> CreateOpenPackageViaAsync(ProcurementPackageController creator, PEMSDbContext ctx, bool requiresInspection = false)
    {
        await creator.Create("PP-001", 1, null, "Title", null);
        var package = ctx.ProcurementPackages.Single();
        package.RequiresInspection = requiresInspection;
        await ctx.SaveChangesAsync();
        return package.ProcurementPackageId;
    }

    [Fact]
    public async Task AdvanceStage_ToCloseout_ViaController_ByEditor_DoesNotClose_AndReportsError()
    {
        var ctx = await CreateSeededDbAsync(nameof(AdvanceStage_ToCloseout_ViaController_ByEditor_DoesNotClose_AndReportsError));
        var creator = await ControllerForAsync(ctx, "creator", SystemRoles.ContractManager);
        var id = await CreateOpenPackageViaAsync(creator, ctx, requiresInspection: true);

        var result = await creator.AdvanceStage(id, "Closeout");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(creator.TempData["Error"]);
        var package = ctx.ProcurementPackages.Single();
        Assert.NotEqual("Closed", package.Status);
        Assert.Null(package.ClosedDate);
    }

    [Fact]
    public async Task AdvanceStage_ByUserWithoutEdit_IsForbidden_AndNothingChanges()
    {
        var ctx = await CreateSeededDbAsync(nameof(AdvanceStage_ByUserWithoutEdit_IsForbidden_AndNothingChanges));
        var creator = await ControllerForAsync(ctx, "creator", SystemRoles.ContractManager);
        var id = await CreateOpenPackageViaAsync(creator, ctx);
        var viewer = await ControllerForAsync(ctx, "viewer", SystemRoles.Viewer);

        var result = await viewer.AdvanceStage(id, "MRQ");

        Assert.IsType<ForbidResult>(result);
        Assert.Equal("Open", ctx.ProcurementPackages.Single().Status);
    }

    [Fact]
    public async Task AdvanceStage_NormalTransition_ViaController_StillWorks()
    {
        var ctx = await CreateSeededDbAsync(nameof(AdvanceStage_NormalTransition_ViaController_StillWorks));
        var creator = await ControllerForAsync(ctx, "creator", SystemRoles.ContractManager);
        var id = await CreateOpenPackageViaAsync(creator, ctx);

        var result = await creator.AdvanceStage(id, "MRQ");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null(creator.TempData["Error"]);
        Assert.Equal("MRQ", ctx.ProcurementPackages.Single().Stage);
    }

    [Fact]
    public async Task Closeout_ByUserWithoutApprove_IsForbidden_AndNothingChanges()
    {
        var ctx = await CreateSeededDbAsync(nameof(Closeout_ByUserWithoutApprove_IsForbidden_AndNothingChanges));
        var creator = await ControllerForAsync(ctx, "creator", SystemRoles.ContractManager);
        var id = await CreateOpenPackageViaAsync(creator, ctx);
        var viewer = await ControllerForAsync(ctx, "viewer", SystemRoles.Viewer);

        var result = await viewer.Closeout(id);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal("Open", ctx.ProcurementPackages.Single().Status);
    }

    [Fact]
    public async Task Closeout_BySameUserWhoCreatedThePackage_IsDeniedBySoD_AndNothingChanges()
    {
        var ctx = await CreateSeededDbAsync(nameof(Closeout_BySameUserWhoCreatedThePackage_IsDeniedBySoD_AndNothingChanges));
        var creator = await ControllerForAsync(ctx, "creator", SystemRoles.ContractManager);
        var id = await CreateOpenPackageViaAsync(creator, ctx);

        var result = await creator.Closeout(id);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(creator.TempData["Error"]);
        Assert.Equal("Open", ctx.ProcurementPackages.Single().Status);
    }

    [Fact]
    public async Task Closeout_ByIndependentApprover_ClosesThePackage()
    {
        var ctx = await CreateSeededDbAsync(nameof(Closeout_ByIndependentApprover_ClosesThePackage));
        var creator = await ControllerForAsync(ctx, "creator", SystemRoles.ContractManager);
        var id = await CreateOpenPackageViaAsync(creator, ctx);
        var approver = await ControllerForAsync(ctx, "approver", SystemRoles.EngineeringManager);

        var result = await approver.Closeout(id);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null(approver.TempData["Error"]);
        var package = ctx.ProcurementPackages.Single();
        Assert.Equal("Closed", package.Status);
        Assert.Equal("Closeout", package.Stage);
    }

    [Fact]
    public async Task Closeout_ByIndependentApprover_WithIncompleteInspection_IsBlocked()
    {
        var ctx = await CreateSeededDbAsync(nameof(Closeout_ByIndependentApprover_WithIncompleteInspection_IsBlocked));
        var creator = await ControllerForAsync(ctx, "creator", SystemRoles.ContractManager);
        var id = await CreateOpenPackageViaAsync(creator, ctx, requiresInspection: true);
        ctx.InspectionTestPlans.Add(new InspectionTestPlan { ProcurementPackageId = id, Status = "Open" });
        await ctx.SaveChangesAsync();
        var approver = await ControllerForAsync(ctx, "approver", SystemRoles.EngineeringManager);

        var result = await approver.Closeout(id);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(approver.TempData["Error"]);
        Assert.NotEqual("Closed", ctx.ProcurementPackages.Single().Status);
    }
}
