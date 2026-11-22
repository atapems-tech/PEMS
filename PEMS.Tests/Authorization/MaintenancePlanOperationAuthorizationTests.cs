using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Services;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Phase 6B Wave B1 — ممیزی مستقل نهایی» بند P0: MaintenancePlanController هیچ
/// بررسی مجوزی نداشت - هر کاربر احرازهویت‌شده‌ای می‌توانست Plan بسازد، از آن WorkOrder بسازد،
/// یا آن را غیرفعال کند.
/// </summary>
public class MaintenancePlanOperationAuthorizationTests
{
    // NumberingService واقعی از UPDATE...OUTPUT خام SQL Server استفاده می‌کند (با InMemory کار
    // نمی‌کند) - این Fake ساده فقط برای اثبات Forbid/موفقیت مسیر مجوز لازم است، نه خودِ منطق شماره‌گذاری.
    private class FakeNumberingService : INumberingService
    {
        public Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null) =>
            Task.FromResult($"{documentType}-0001");
    }

    // طبق «PEMS Phase 6»: این تست‌ها فقط مسیر مجوز را می‌سنجند (Forbid قبل از رسیدن به سرویس) -
    // یک Fake ساده کافی است، نه پیاده‌سازی واقعی Idempotency.
    private class FakePMGenerationService : IPMGenerationService
    {
        public Task<string?> GenerateForPlanAsync(int planId) => Task.FromResult<string?>("WO-0001");
        public Task<PEMS.Application.DTOs.PMGenerationResultDto> GenerateDueWorkOrdersAsync() =>
            Task.FromResult(new PEMS.Application.DTOs.PMGenerationResultDto());
        public Task<int> ReconcileFloatingSchedulesAsync() => Task.FromResult(0);
    }

    private static async Task<(MaintenancePlanOperations Operations, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(
        string dbName, string? role = SystemRoles.MaintenanceEngineer)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"MaintPlanAuth_{dbName}");
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

        var operations = new MaintenancePlanOperations(new MaintenancePlanService(ctx), new FakePMGenerationService(), null!, ctx,
            new MaintenancePlanCommandService(ctx, new FakeNumberingService()), new StrategyAssignmentService(ctx));
        return (operations, ctx);
    }

    private static async Task<int> SeedAssignmentAsync(PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var eq = new Equipment { EquipmentId = 1, TagNumber = "EQ-001", Name = "EQ-001", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(eq);
        var strategy = new MaintenanceStrategy
        {
            StrategyType = "Preventive", ConsequenceCategory = "Operational", Status = "Active", IsActive = true, FailureModeCode = "X"
        };
        ctx.MaintenanceStrategies.Add(strategy);
        await ctx.SaveChangesAsync();
        var assignment = new MaintenanceStrategyAssignment { MaintenanceStrategyId = strategy.MaintenanceStrategyId, EquipmentId = eq.EquipmentId, IsActive = true };
        ctx.MaintenanceStrategyAssignments.Add(assignment);
        ctx.JobPlans.Add(new JobPlan { JobPlanId = 1, JobPlanNumber = "JP-1", Title = "JP", IsActive = true });
        await ctx.SaveChangesAsync();
        return assignment.MaintenanceStrategyAssignmentId;
    }

    [Fact]
    public void The_create_operations_require_the_permission_the_controller_checked_and_the_engine_checks_it_before_the_call()
    {
        var catalog = PEMS.Web.Definitions.OperationCatalog.Scan(typeof(PEMS.Application.Common.PemsOperationAttribute).Assembly);
        foreach (var action in new[] { "Create", "FormOf", "AssignmentChoices", "JobPlanChoices" })
            Assert.Equal("MaintenancePlan.Create", catalog.Get("MaintenancePlanPage", action)!.Permission);
    }

    [Fact]
    public async Task Create_WithCreatePermission_CreatesMaintenancePlan_FromTheAssignment()
    {
        var (operations, ctx) = await CreateController(nameof(Create_WithCreatePermission_CreatesMaintenancePlan_FromTheAssignment));
        var assignmentId = await SeedAssignmentAsync(ctx);

        var result = await operations.CreateAsync(new PEMS.Application.DTOs.PlanInputDto
        {
            MaintenanceStrategyAssignmentId = assignmentId, Title = "Test Plan", TriggerType = "Calendar", IntervalDays = 30, JobPlanId = 1
        }, new List<string> { SystemRoles.MaintenanceEngineer }, null, "tester");

        Assert.True(result.Success);
        var plan = Assert.Single(ctx.MaintenancePlans);
        Assert.Equal(plan.MaintenancePlanId, result.NewId);
        Assert.Equal(1, plan.EquipmentId); // the equipment is derived from the assignment, never from the client
    }

    [Fact]
    public async Task Create_WithAnUnknownAssignment_GivesTheFirstRuleErrorAndCreatesNothing()
    {
        var (operations, ctx) = await CreateController(nameof(Create_WithAnUnknownAssignment_GivesTheFirstRuleErrorAndCreatesNothing));

        var result = await operations.CreateAsync(new PEMS.Application.DTOs.PlanInputDto { MaintenanceStrategyAssignmentId = 424242, Title = "Test Plan", TriggerType = "Calendar", IntervalDays = 30, JobPlanId = 1 },
            new List<string> { SystemRoles.MaintenanceEngineer }, null, "tester");

        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.Error));
        Assert.Empty(ctx.MaintenancePlans);
    }
}
