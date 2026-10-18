using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Persistence.Seed;
using PEMS.Tests.TestSupport;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Tests.Workflow;

/// <summary>
/// اطمینان از این‌که تعریف‌های Seed شده‌ی ماژول‌های موجود با افزودن Guard و Validator نمی‌شکنند و
/// همچنان بدون Binding (مسیر Legacy) قابل شروع‌اند. هیچ ماژولی به Runtime جدید مهاجرت داده نشده است.
/// </summary>
public class WorkflowLegacyCompatibilityTests : IDisposable
{
    private readonly PEMSDbContext _ctx = PemsDbContextFactory.CreateInMemory("wf-legacy-" + Guid.NewGuid());
    public void Dispose() => _ctx.Dispose();

    private async Task SeedAllAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_ctx);
        var provider = services.BuildServiceProvider();
        await EngineeringDocumentWorkflowSeeder.SeedAsync(provider);
        await MOCWorkflowSeeder.SeedAsync(provider);
        await NcrApprovalWorkflowSeeder.SeedAsync(provider);
        await PermitApprovalWorkflowSeeder.SeedAsync(provider);
        await TechnicalQueryWorkflowSeeder.SeedAsync(provider);
        await VdrWorkflowSeeder.SeedAsync(provider);
        await WorkOrderReviewWorkflowSeeder.SeedAsync(provider);
    }

    private static WorkflowActionRegistry LegacyRegistry()
    {
        var r = new WorkflowActionRegistry();
        foreach (var (m, actions) in new (string, string[])[]
        {
            ("EngineeringDocument", new[] { "Issue" }), ("MOC", new[] { "Assess", "Close" }),
            ("TechnicalQuery", new[] { "Confirm", "Reopen", "Respond" }), ("VendorDocument", new[] { "AC", "AP", "CO", "NC", "RE", "Issue" })
        })
            foreach (var a in actions) r.RegisterModuleAction(m, a, WorkflowActionKind.Outcome, "legacy");
        return r;
    }

    [Fact]
    public async Task All_seeded_definitions_still_seed_and_stay_published_and_immutable()
    {
        await SeedAllAsync();
        var versions = await _ctx.WorkflowVersions.ToListAsync();
        Assert.NotEmpty(versions);
        Assert.Contains(versions, v => v.Status == "Published");

        var published = versions.First(v => v.Status == "Published");
        var stage = await _ctx.WorkflowStages.FirstAsync(s => s.WorkflowVersionId == published.WorkflowVersionId);
        stage.Name += "!";
        await Assert.ThrowsAsync<WorkflowImmutableVersionException>(() => _ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Seeded_definitions_pass_the_validator_with_no_structural_or_runtime_errors()
    {
        await SeedAllAsync();
        var validator = new WorkflowVersionValidator(_ctx, LegacyRegistry(), new WorkflowConditionEvaluator(), catalog: null);
        var report = new List<string>();
        foreach (var v in await _ctx.WorkflowVersions.Where(x => x.Status == "Published").ToListAsync())
        {
            var r = await validator.ValidateAsync(v.WorkflowVersionId);
            report.AddRange(r.Errors.Select(e => $"v{v.WorkflowVersionId}: {e.Code} {e.Message}"));
        }
        // گزارش را به‌صورت شفاف در پیام شکست می‌آوریم؛ هرچه باشد باید در مستندات «گزارش مهاجرت» ثبت شود.
        Assert.True(report.Count == 0, string.Join(Environment.NewLine, report));
    }

    [Fact]
    public async Task Legacy_start_and_execute_work_for_seeded_definitions_without_any_binding()
    {
        await SeedAllAsync();
        var runtime = new WorkflowRuntimeService(_ctx);
        var id = await runtime.StartInstanceAsync("WorkOrder", "WorkOrder", 1, WorkOrderReviewWorkflowSeeder.Category, null, 1, new[] { "Discipline:1" });
        var inst = await _ctx.WorkflowInstances.FirstAsync(i => i.WorkflowInstanceId == id);
        Assert.Null(inst.WorkflowBindingId);
        Assert.Equal("Running", inst.Status);
    }
}
