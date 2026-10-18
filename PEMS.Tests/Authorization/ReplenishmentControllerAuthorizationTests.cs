using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Replenishment;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Areas.Warehouse.Controllers;

namespace PEMS.Tests.Authorization;

/// <summary>
/// Phase 2 / P5 - مجوزهای ReplenishmentController با موتور مجوز واقعی (RolePermission → AccessControlService): View / Create (اجرا) / Edit (رد/بازگشایی).
/// هیچ Grant خودکاری وجود ندارد؛ هر نقش فقط همان سطری را دارد که تست می‌سازد. ردها در AuthorizationAuditLog ثبت می‌شوند.
/// </summary>
public class ReplenishmentControllerAuthorizationTests
{
    private const string Module = "ReplenishmentRecommendation";

    private static async Task<(ReplenishmentController Controller, PEMSDbContext Ctx)> CreateAsync(string dbName, bool view = false, bool create = false, bool edit = false, bool anyRole = true)
    {
        var ctx = PemsDbContextFactory.CreateInMemory("ReplCtrlAuth_" + dbName);
        ctx.Roles.Add(new ApplicationRole("REPL_TEST") { NormalizedName = "REPL_TEST" });
        if (anyRole)
            ctx.RolePermissions.Add(new RolePermission { RoleName = "REPL_TEST", ModuleName = Module, CanView = view, CanCreate = create, CanEdit = edit, CreatedDate = DateTime.Now });
        ctx.Warehouses.Add(new Warehouse { Code = "WH-1", Name = "Main" });
        ctx.MaterialItems.Add(new MaterialItem { MaterialCode = "M-1", Description = "Bearing" });
        await ctx.SaveChangesAsync();

        // ProtectedActionGuard مسیر آگاه-از-کاربر را هم می‌سنجد ⇒ کاربر واقعی با نقش مستقیم لازم است
        var user = new ApplicationUser { UserName = "repl.user", Email = "repl.user@x.local", FullName = "Repl User" };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        if (anyRole)
        {
            ctx.UserRoles.Add(new IdentityUserRole<int> { UserId = user.Id, RoleId = ctx.Roles.First(r => r.Name == "REPL_TEST").Id });
            await ctx.SaveChangesAsync();
        }

        var access = new AccessControlService(ctx);
        var permissions = new PermissionService(ctx, access);
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());

        var currentUser = new FakeCurrentUserService { UserId = user.Id, Roles = anyRole ? new List<string> { "REPL_TEST" } : new List<string>() };
        var netDemand = new NetDemandService(new NetDemandCollector(ctx), new UsableStockService(ctx), permissions);
        var service = new ReplenishmentRecommendationService(ctx, netDemand, permissions);
        var controller = new ReplenishmentController(service, permissions, access, currentUser, ctx).WithTempData();
        return (controller, ctx);
    }

    private static async Task<int> SeedRecommendationAsync(PEMSDbContext ctx, string status = "Open")
    {
        var run = new NetDemandRun { Status = "Completed", CalculatorVersion = 1 };
        ctx.NetDemandRuns.Add(run);
        await ctx.SaveChangesAsync();
        var rec = new ReplenishmentRecommendation
        {
            MaterialItemId = 1, WarehouseId = 1, Status = status, ShortageQuantity = 5, RecommendedQuantity = 5,
            FirstRunId = run.NetDemandRunId, LastRunId = run.NetDemandRunId, InputFingerprint = new string('b', 64)
        };
        ctx.ReplenishmentRecommendations.Add(rec);
        await ctx.SaveChangesAsync();
        return rec.ReplenishmentRecommendationId;
    }

    [Fact]
    public async Task Index_WithoutViewPermission_IsForbidden()
    {
        var (controller, _) = await CreateAsync(nameof(Index_WithoutViewPermission_IsForbidden), anyRole: false);

        Assert.IsType<ForbidResult>(await controller.Index(null, null));
    }

    [Fact]
    public async Task Index_AndDetails_WithViewPermission_Render()
    {
        var (controller, ctx) = await CreateAsync(nameof(Index_AndDetails_WithViewPermission_Render), view: true);
        var id = await SeedRecommendationAsync(ctx);

        Assert.IsType<ViewResult>(await controller.Index(null, null));
        Assert.IsType<ViewResult>(await controller.Details(id));
        Assert.IsType<NotFoundResult>(await controller.Details(9999));
    }

    [Fact]
    public async Task Run_WithViewButWithoutCreate_IsForbidden_NothingRuns_AndTheDenialIsAudited()
    {
        var (controller, ctx) = await CreateAsync(nameof(Run_WithViewButWithoutCreate_IsForbidden_NothingRuns_AndTheDenialIsAudited), view: true);

        var result = await controller.Run("k1");

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(await ctx.NetDemandRuns.ToListAsync());
        var audit = await ctx.AuthorizationAuditLogs.Where(a => a.ResourceName == Module && a.ActionName == "Create").ToListAsync();
        Assert.Single(audit);
        Assert.Equal("Deny", audit[0].Decision.Length > 0 && audit[0].Decision.StartsWith("Den") ? "Deny" : audit[0].Decision);
    }

    [Fact]
    public async Task Run_WithCreatePermission_ExecutesOnce_AndTheSameRequestKeyIsAReplay()
    {
        var (controller, ctx) = await CreateAsync(nameof(Run_WithCreatePermission_ExecutesOnce_AndTheSameRequestKeyIsAReplay), view: true, create: true);

        Assert.IsType<RedirectToActionResult>(await controller.Run("same-key"));
        Assert.IsType<RedirectToActionResult>(await controller.Run("same-key"));

        Assert.Equal(1, await ctx.NetDemandRuns.CountAsync());
    }

    [Fact]
    public async Task Dismiss_RequiresEdit_ViewAndCreateAreNotEnough()
    {
        var (controller, ctx) = await CreateAsync(nameof(Dismiss_RequiresEdit_ViewAndCreateAreNotEnough), view: true, create: true);
        var id = await SeedRecommendationAsync(ctx);

        var result = await controller.Dismiss(id, null, "no need");

        Assert.IsType<ForbidResult>(result);
        Assert.Equal("Open", (await ctx.ReplenishmentRecommendations.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task DismissAndReopen_WithEditPermission_ChangeTheStatusAndWriteHistory()
    {
        var (controller, ctx) = await CreateAsync(nameof(DismissAndReopen_WithEditPermission_ChangeTheStatusAndWriteHistory), view: true, edit: true);
        var id = await SeedRecommendationAsync(ctx);

        Assert.IsType<RedirectToActionResult>(await controller.Dismiss(id, null, "covered elsewhere"));
        Assert.Equal("Dismissed", (await ctx.ReplenishmentRecommendations.AsNoTracking().SingleAsync()).Status);
        Assert.IsType<RedirectToActionResult>(await controller.Reopen(id, null, "needed"));
        Assert.Equal("Open", (await ctx.ReplenishmentRecommendations.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(new[] { "Dismissed", "Reopened" }, (await ctx.ReplenishmentRecommendationHistories.OrderBy(h => h.ReplenishmentRecommendationHistoryId).ToListAsync()).Select(h => h.Action).ToArray());
    }
}
