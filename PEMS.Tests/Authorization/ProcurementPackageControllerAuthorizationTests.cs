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
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());

        var currentUser = new FakeCurrentUserService { Roles = role != null ? new List<string> { role } : new List<string>() };

        var service = new ProcurementPackageService(ctx);
        var controller = new ProcurementPackageController(service, ctx, currentUser, permissionService).WithTempData();
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
}
