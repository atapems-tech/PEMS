using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Phase 7.6»: ماژول مجوز مجزای "CriticalityAssessment" - Viewer فقط View، Editor
/// (MaintenanceEngineer) Create/Edit ولی هرگز Approve، Approve فقط Supervisor/Manager.
/// </summary>
public class CriticalityAssessmentControllerAuthorizationTests
{
    private static async Task<(PEMS.Persistence.Context.PEMSDbContext Ctx, PermissionService Perms)> CreateInfra(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"CritAssessAuth_{dbName}");
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();

        var permissionService = new PermissionService(ctx, new AccessControlService(ctx));
        await permissionService.SeedDefaultsAsync();

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());
        return (ctx, permissionService);
    }

    private static async Task<bool> MayAsync(PermissionService perms, string? role, string permission)
    {
        var parts = permission.Split('.');
        return await perms.HasPermissionAsync(role != null ? new List<string> { role } : new List<string>(), parts[0], parts[1]);
    }

    // SCRUM-701: the workflow commands are operations; the host refuses a caller without the operation permission, then calls the operation.
    private static async Task<(bool Allowed, (bool Success, string? Error) Outcome)> RunAsync(string dbName, string role, string permission,
        Func<PEMS.Web.Services.CriticalityAssessmentOperations, Task<(bool Success, string? Error)>> call, PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var permissionService = new PermissionService(ctx, new AccessControlService(ctx));
        var parts = permission.Split('.');
        if (!await permissionService.HasPermissionAsync(new List<string> { role }, parts[0], parts[1])) return (false, default);
        var ops = new PEMS.Web.Services.CriticalityAssessmentOperations(new CriticalityAssessmentService(ctx), new FakeCurrentUserService { Roles = new List<string> { role }, UserName = "tester" });
        return (true, await call(ops));
    }

    [Fact]
    public async Task Dashboard_NoPermission_IsNotAllowed()
    {
        var (_, perms) = await CreateInfra(nameof(Dashboard_NoPermission_IsNotAllowed));
        Assert.False(await MayAsync(perms, null, "CriticalityAssessment.View"));
    }

    [Fact]
    public async Task Dashboard_ViewerRole_CanView_AndTheCountsAreReturned()
    {
        var (ctx, perms) = await CreateInfra(nameof(Dashboard_ViewerRole_CanView_AndTheCountsAreReturned));
        Assert.True(await MayAsync(perms, SystemRoles.Viewer, "CriticalityAssessment.View"));
        var ops = new PEMS.Web.Services.CriticalityAssessmentOperations(new CriticalityAssessmentService(ctx), new FakeCurrentUserService());
        Assert.Single(await ops.DashboardAsync());
    }

    [Fact]
    public async Task Queue_NoPermission_IsNotAllowed_AndAnEmptyQueueIsEmpty()
    {
        var (ctx, perms) = await CreateInfra(nameof(Queue_NoPermission_IsNotAllowed_AndAnEmptyQueueIsEmpty));
        Assert.False(await MayAsync(perms, null, "CriticalityAssessment.View"));
        var ops = new PEMS.Web.Services.CriticalityAssessmentOperations(new CriticalityAssessmentService(ctx), new FakeCurrentUserService());
        Assert.Empty(await ops.QueueAsync());
    }

    [Fact]
    public async Task Create_ViewerRole_IsNotAllowed_NoAssessmentCreated()
    {
        var (ctx, perms) = await CreateInfra(nameof(Create_ViewerRole_IsNotAllowed_NoAssessmentCreated));
        Assert.False(await MayAsync(perms, SystemRoles.Viewer, "CriticalityAssessment.Create"));
        Assert.Empty(ctx.CriticalityAssessments);
    }

    [Fact]
    public async Task Create_MaintenanceEngineer_CanCreate()
    {
        var (ctx, perms) = await CreateInfra(nameof(Create_MaintenanceEngineer_CanCreate));
        var equipment = new Equipment { TagNumber = "EQ-2", Name = "EQ-2", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();

        Assert.True(await MayAsync(perms, SystemRoles.MaintenanceEngineer, "CriticalityAssessment.Create"));
        var ops = new PEMS.Web.Services.CriticalityAssessmentOperations(new CriticalityAssessmentService(ctx), new FakeCurrentUserService { UserName = "tester" });
        var result = await ops.CreateDraftAsync(equipment.EquipmentId, "rationale");

        Assert.True(result.Success);
        Assert.Single(ctx.CriticalityAssessments);
    }

    [Fact]
    public async Task Approve_MaintenanceEngineer_ReturnsForbid_StatusUnchanged()
    {
        // طبق بند ۱۱: Editor (MaintenanceEngineer) هرگز نمی‌تواند تایید کند - فقط Supervisor/Manager
        var (ctx, _) = await CreateInfra(nameof(Approve_MaintenanceEngineer_ReturnsForbid_StatusUnchanged));
        var equipment = new Equipment { TagNumber = "EQ-3", Name = "EQ-3", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        var assessment = new CriticalityAssessment
        {
            EquipmentId = equipment.EquipmentId, Status = "UNDER_REVIEW", PreparedBy = "engineer1",
            SafetyScore = 3, ProductionScore = 3, EnvironmentScore = 3, IsActive = true, CreatedDate = System.DateTime.Now
        };
        ctx.CriticalityAssessments.Add(assessment);
        await ctx.SaveChangesAsync();

        var result = await RunAsync("approve-eng", SystemRoles.MaintenanceEngineer, "CriticalityAssessment.Approve", o => o.ApproveAsync(assessment.CriticalityAssessmentId), ctx);

        Assert.False(result.Allowed);
        Assert.Equal("UNDER_REVIEW", ctx.CriticalityAssessments.Single().Status);
        Assert.Empty(ctx.EquipmentCriticalities);
    }

    [Fact]
    public async Task Approve_EngineeringSupervisor_DifferentPreparer_CanApprove_PublishesResult()
    {
        var (ctx, _) = await CreateInfra(nameof(Approve_EngineeringSupervisor_DifferentPreparer_CanApprove_PublishesResult));
        var equipment = new Equipment { TagNumber = "EQ-4", Name = "EQ-4", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        var assessment = new CriticalityAssessment
        {
            EquipmentId = equipment.EquipmentId, Status = "UNDER_REVIEW", PreparedBy = "engineer1",
            SafetyScore = 4, ProductionScore = 3, EnvironmentScore = 2, IsActive = true, CreatedDate = System.DateTime.Now
        };
        ctx.CriticalityAssessments.Add(assessment);
        await ctx.SaveChangesAsync();

        var result = await RunAsync("approve-sup", SystemRoles.EngineeringSupervisor, "CriticalityAssessment.Approve", o => o.ApproveAsync(assessment.CriticalityAssessmentId), ctx);

        Assert.True(result.Allowed);
        Assert.True(result.Outcome.Success);
        Assert.Equal("APPROVED", ctx.CriticalityAssessments.Single().Status);
        Assert.Single(ctx.EquipmentCriticalities);
    }

    [Fact]
    public async Task Reject_MaintenanceEngineer_ReturnsForbid()
    {
        var (ctx, _) = await CreateInfra(nameof(Reject_MaintenanceEngineer_ReturnsForbid));
        var equipment = new Equipment { TagNumber = "EQ-5", Name = "EQ-5", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        var assessment = new CriticalityAssessment { EquipmentId = equipment.EquipmentId, Status = "UNDER_REVIEW", IsActive = true, CreatedDate = System.DateTime.Now };
        ctx.CriticalityAssessments.Add(assessment);
        await ctx.SaveChangesAsync();

        var result = await RunAsync("reject-eng", SystemRoles.MaintenanceEngineer, "CriticalityAssessment.Approve", o => o.RejectAsync(assessment.CriticalityAssessmentId), ctx);

        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task SetScores_ViewerRole_ReturnsForbid()
    {
        var (ctx, _) = await CreateInfra(nameof(SetScores_ViewerRole_ReturnsForbid));
        var equipment = new Equipment { TagNumber = "EQ-6", Name = "EQ-6", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        var assessment = new CriticalityAssessment { EquipmentId = equipment.EquipmentId, Status = "DRAFT", IsActive = true, CreatedDate = System.DateTime.Now };
        ctx.CriticalityAssessments.Add(assessment);
        await ctx.SaveChangesAsync();

        var result = await RunAsync("scores-viewer", SystemRoles.Viewer, "CriticalityAssessment.Edit", o => o.SetScoresAsync(assessment.CriticalityAssessmentId, 3, 3, 3, null), ctx);

        Assert.False(result.Allowed);
        Assert.Null(ctx.CriticalityAssessments.Single().SafetyScore);
    }
}
