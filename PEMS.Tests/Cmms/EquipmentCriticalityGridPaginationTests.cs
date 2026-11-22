using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Application.Interfaces;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B — Wave A: EquipmentCriticality Grid» + ممیزی مستقل (P0). قبلاً کل جدول
/// فیلترشده (با Include) با ToListAsync (بدون Skip/Take) بارگذاری می‌شد؛ شمارنده‌های Dashboard
/// (ClassA/B/C، Unassessed) هم با یک بارگذاری کامل دوم در حافظه محاسبه می‌شدند - اکنون با
/// GroupBy/EXISTS در SQL. طبق ممیزی مستقل: Index/Assess قبلاً هیچ Permission gate ای نداشتند -
/// این‌جا با HasPermissionAsync("EquipmentCriticality", ...) اصلاح شده.
/// </summary>
public class EquipmentCriticalityGridPaginationTests
{
    private static async Task<(PEMS.Web.Services.RegisterPagesOperations Controller, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(
        string dbName, string? role = SystemRoles.MaintenanceEngineer)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"EqCritGrid_{dbName}");
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "TP", ProjectName = "Test Project" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "MECH", Name = "Mechanical" });
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

        var controller = new PEMS.Web.Services.RegisterPagesOperations(ctx, null!, null!);
        return (controller, ctx);
    }

    private static int _nextId = 1;

    private static Equipment NewEquipment(string tag, bool isActive = true)
    {
        var id = _nextId++;
        return new Equipment { EquipmentId = id, TagNumber = tag, Name = tag, EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1, IsActive = isActive };
    }

    private static EquipmentCriticality NewCriticality(int equipmentId, string criticalityClass = "A") => new()
    {
        EquipmentCriticalityId = _nextId++,
        EquipmentId = equipmentId,
        CriticalityClass = criticalityClass,
        IsActive = true,
        CreatedDate = DateTime.Now
    };

    // SCRUM-693: the list is a read operation (the grid pages in the browser); the gate is the operation permission EquipmentCriticality.View
    private static async Task<List<CriticalityRow>> RunIndex(PEMS.Web.Services.RegisterPagesOperations ops, string? criticalityClass = null) => await ops.CriticalityListAsync(criticalityClass);

    [Fact]
    public async Task Index_ClassFilter_StillWorksAfterPageSizeChange()
    {
        var (controller, ctx) = await CreateController(nameof(Index_ClassFilter_StillWorksAfterPageSizeChange));
        var eqA = NewEquipment("TAG-A");
        var eqB = NewEquipment("TAG-B");
        ctx.Equipments.AddRange(eqA, eqB);
        ctx.EquipmentCriticalities.Add(NewCriticality(eqA.EquipmentId, "A"));
        ctx.EquipmentCriticalities.Add(NewCriticality(eqB.EquipmentId, "B"));
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller, criticalityClass: "A");

        Assert.Single(result);
        Assert.Equal("A", result[0].CriticalityClass);
    }

    [Fact]
    public async Task Index_InactiveCriticalities_AreExcluded()
    {
        var (controller, ctx) = await CreateController(nameof(Index_InactiveCriticalities_AreExcluded));
        var eq1 = NewEquipment("TAG-ACTIVE");
        var eq2 = NewEquipment("TAG-INACTIVE");
        ctx.Equipments.AddRange(eq1, eq2);
        var active = NewCriticality(eq1.EquipmentId);
        var inactive = NewCriticality(eq2.EquipmentId);
        inactive.IsActive = false;
        ctx.EquipmentCriticalities.Add(active);
        ctx.EquipmentCriticalities.Add(inactive);
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller);

        Assert.Single(result);
        Assert.Equal(active.EquipmentCriticalityId, result[0].Id);
    }

    [Fact]
    public async Task Index_EmptyDataset_ReturnsEmptyPagedResult()
    {
        var (controller, _) = await CreateController(nameof(Index_EmptyDataset_ReturnsEmptyPagedResult));

        var result = await RunIndex(controller);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Index_DashboardCounts_AreGlobal_NotRestrictedByClassFilter()
    {
        var (controller, ctx) = await CreateController(nameof(Index_DashboardCounts_AreGlobal_NotRestrictedByClassFilter));
        var eqA1 = NewEquipment("TAG-A1");
        var eqA2 = NewEquipment("TAG-A2");
        var eqB = NewEquipment("TAG-B");
        var eqUnassessed = NewEquipment("TAG-UNASSESSED");
        ctx.Equipments.AddRange(eqA1, eqA2, eqB, eqUnassessed);
        ctx.EquipmentCriticalities.Add(NewCriticality(eqA1.EquipmentId, "A"));
        ctx.EquipmentCriticalities.Add(NewCriticality(eqA2.EquipmentId, "A"));
        ctx.EquipmentCriticalities.Add(NewCriticality(eqB.EquipmentId, "B"));
        await ctx.SaveChangesAsync();

        var counts = Assert.Single(await controller.CriticalityCountsAsync());

        Assert.Equal(2, counts.ClassA);
        Assert.Equal(1, counts.ClassB);   // independent of the class filter of the list
        Assert.Equal(0, counts.ClassC);
        Assert.Equal(1, counts.Unassessed);
    }


    [Fact]
    public async Task Assess_NonexistentEquipment_DoesNotCreateAssessment()
    {
        var (controller, ctx) = await CreateController(nameof(Assess_NonexistentEquipment_DoesNotCreateAssessment));

        var result = await new PEMS.Web.Services.ReliabilityRegisterOperations(ctx, null!, null!, new FakeCurrentUserService()).AssessCriticalityAsync(999, "A", null, null, null, null, null);

        Assert.False(result.Success);
        Assert.Empty(ctx.EquipmentCriticalities);
    }

    [Fact]
    public async Task Assess_InactiveEquipment_DoesNotCreateAssessment()
    {
        var (controller, ctx) = await CreateController(nameof(Assess_InactiveEquipment_DoesNotCreateAssessment));
        var eq = NewEquipment("TAG-INACTIVE", isActive: false);
        ctx.Equipments.Add(eq);
        await ctx.SaveChangesAsync();

        var result = await new PEMS.Web.Services.ReliabilityRegisterOperations(ctx, null!, null!, new FakeCurrentUserService()).AssessCriticalityAsync(eq.EquipmentId, "A", null, null, null, null, null);

        Assert.False(result.Success);
        Assert.Empty(ctx.EquipmentCriticalities);
    }

    [Fact]
    public async Task Assess_ActiveEquipment_CreatesAssessment()
    {
        var (controller, ctx) = await CreateController(nameof(Assess_ActiveEquipment_CreatesAssessment));
        var eq = NewEquipment("TAG-001");
        ctx.Equipments.Add(eq);
        await ctx.SaveChangesAsync();

        var result = await new PEMS.Web.Services.ReliabilityRegisterOperations(ctx, null!, null!, new FakeCurrentUserService()).AssessCriticalityAsync(eq.EquipmentId, "A", "High", null, null, null, null);

        Assert.True(result.Success, result.Error);
        var created = Assert.Single(ctx.EquipmentCriticalities);
        Assert.Equal(eq.EquipmentId, created.EquipmentId);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task Assess_ExistingActiveAssessment_ReplacesNotDuplicates()
    {
        var (controller, ctx) = await CreateController(nameof(Assess_ExistingActiveAssessment_ReplacesNotDuplicates));
        var eq = NewEquipment("TAG-001");
        ctx.Equipments.Add(eq);
        var previous = NewCriticality(eq.EquipmentId, "B");
        ctx.EquipmentCriticalities.Add(previous);
        await ctx.SaveChangesAsync();

        await new PEMS.Web.Services.ReliabilityRegisterOperations(ctx, null!, null!, new FakeCurrentUserService()).AssessCriticalityAsync(eq.EquipmentId, "A", null, null, null, null, null);

        var all = ctx.EquipmentCriticalities.ToList();
        Assert.Equal(2, all.Count);
        Assert.False(all.Single(x => x.EquipmentCriticalityId == previous.EquipmentCriticalityId).IsActive);
        Assert.Single(all, x => x.IsActive);
    }
}
