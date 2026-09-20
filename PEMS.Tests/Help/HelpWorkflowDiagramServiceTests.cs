using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Tests.Help;

/// <summary>
/// طبق «PEMS Help System — Phase 3» بخش ۱۵ (Workflow-Accuracy Tests): تست‌های این کلاس تایید
/// می‌کنند که HelpWorkflowDiagramService (Workflow Graph Adapter) دقیقاً همان چیزی را از
/// WorkflowStage/WorkflowTransition می‌خواند و برمی‌گرداند که واقعاً در دیتابیس هست - هرگز چیزی
/// جعل/حدس نمی‌زند، فقط از آخرین نسخه‌ی Published استفاده می‌کند، و برچسب فارسی فقط برای
/// نام‌های از‌قبل‌تاییدشده اعمال می‌شود (نامی که در دیکشنری نیست، دست‌نخورده می‌ماند).
/// </summary>
public class HelpWorkflowDiagramServiceTests
{
    private static PEMSDbContext CreateContext(string name) =>
        PemsDbContextFactory.CreateInMemory($"HelpWorkflowDiagram_{name}");

    private static async Task<(WorkflowDefinition def, WorkflowVersion version)> SeedWorkflowAsync(
        PEMSDbContext ctx, string module, string category, int versionNumber, string status)
    {
        var def = new WorkflowDefinition { Name = $"{module}/{category}", Module = module, Category = category, IsActive = true };
        ctx.WorkflowDefinitions.Add(def);
        await ctx.SaveChangesAsync();

        var version = new WorkflowVersion { WorkflowDefinitionId = def.WorkflowDefinitionId, VersionNumber = versionNumber, Status = status };
        ctx.WorkflowVersions.Add(version);
        await ctx.SaveChangesAsync();

        return (def, version);
    }

    [Fact]
    public async Task BuildAsync_UnknownWorkflow_ReturnsNull_NeverFabricates()
    {
        var ctx = CreateContext(nameof(BuildAsync_UnknownWorkflow_ReturnsNull_NeverFabricates));
        var svc = new HelpWorkflowDiagramService(ctx);

        var result = await svc.BuildAsync("NoSuchModule", "NoSuchCategory", "en");

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildAsync_NoPublishedVersion_ReturnsNull()
    {
        var ctx = CreateContext(nameof(BuildAsync_NoPublishedVersion_ReturnsNull));
        await SeedWorkflowAsync(ctx, "TestModule", "TestCategory", 1, "Draft");

        var svc = new HelpWorkflowDiagramService(ctx);
        var result = await svc.BuildAsync("TestModule", "TestCategory", "en");

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildAsync_MultiplePublishedVersions_UsesHighestVersionNumber()
    {
        var ctx = CreateContext(nameof(BuildAsync_MultiplePublishedVersions_UsesHighestVersionNumber));
        // Phase 1 Hardening: نسخه‌ی Published تغییرناپذیر است؛ پس مراحل/گذارها را روی Draft می‌سازیم و بعد منتشر می‌کنیم
        // (همان ترتیب واقعی چرخه‌ی عمر). ادعاهای تست بدون تغییر است.
        var (def, v1) = await SeedWorkflowAsync(ctx, "TestModule", "TestCategory", 1, "Draft");
        var v2 = new WorkflowVersion { WorkflowDefinitionId = def.WorkflowDefinitionId, VersionNumber = 2, Status = "Draft" };
        var v3Draft = new WorkflowVersion { WorkflowDefinitionId = def.WorkflowDefinitionId, VersionNumber = 3, Status = "Draft" };
        ctx.WorkflowVersions.AddRange(v2, v3Draft);
        await ctx.SaveChangesAsync();

        var stageV1 = new WorkflowStage { WorkflowVersionId = v1.WorkflowVersionId, Name = "OldStage", StageOrder = 0 };
        var stageV2 = new WorkflowStage { WorkflowVersionId = v2.WorkflowVersionId, Name = "NewStage", StageOrder = 0 };
        ctx.WorkflowStages.AddRange(stageV1, stageV2);
        await ctx.SaveChangesAsync();
        v1.Status = "Published";
        v2.Status = "Published";
        await ctx.SaveChangesAsync();

        var svc = new HelpWorkflowDiagramService(ctx);
        var result = await svc.BuildAsync("TestModule", "TestCategory", "en");

        Assert.NotNull(result);
        Assert.Equal(2, result!.SourceWorkflowVersion);
        Assert.Single(result.Nodes);
        Assert.Equal("NewStage", result.Nodes[0].Id);
    }

    [Fact]
    public async Task BuildAsync_ReflectsExactStagesAndTransitions_NoInventedEdges()
    {
        var ctx = CreateContext(nameof(BuildAsync_ReflectsExactStagesAndTransitions_NoInventedEdges));
        // Phase 1 Hardening: نسخه‌ی Published تغییرناپذیر است؛ پس مراحل/گذارها را روی Draft می‌سازیم و بعد منتشر می‌کنیم
        // (همان ترتیب واقعی چرخه‌ی عمر). ادعاهای تست بدون تغییر است.
        var (_, version) = await SeedWorkflowAsync(ctx, "TestModule", "TestCategory", 1, "Draft");

        var check = new WorkflowStage { WorkflowVersionId = version.WorkflowVersionId, Name = "Check", StageOrder = 0 };
        var approval = new WorkflowStage { WorkflowVersionId = version.WorkflowVersionId, Name = "Approval", StageOrder = 1 };
        ctx.WorkflowStages.AddRange(check, approval);
        await ctx.SaveChangesAsync();

        // گذار شروع (FromStageId=null) - نباید در خروجی به‌عنوان Edge دیداری ظاهر شود
        ctx.WorkflowTransitions.Add(new WorkflowTransition
        {
            WorkflowVersionId = version.WorkflowVersionId, FromStageId = null, ToStageId = check.WorkflowStageId, ActionType = "Start"
        });
        // گذار عادی
        ctx.WorkflowTransitions.Add(new WorkflowTransition
        {
            WorkflowVersionId = version.WorkflowVersionId, FromStageId = check.WorkflowStageId, ToStageId = approval.WorkflowStageId, ActionType = "Approve"
        });
        // گذار رد (Reject) - باید Kind=reject باشد
        ctx.WorkflowTransitions.Add(new WorkflowTransition
        {
            WorkflowVersionId = version.WorkflowVersionId, FromStageId = approval.WorkflowStageId, ToStageId = check.WorkflowStageId, ActionType = "Reject"
        });
        // گذار پایانی (ToStageId=null) - باید Kind=terminal باشد
        ctx.WorkflowTransitions.Add(new WorkflowTransition
        {
            WorkflowVersionId = version.WorkflowVersionId, FromStageId = approval.WorkflowStageId, ToStageId = null, ActionType = "Issue"
        });
        await ctx.SaveChangesAsync();

        version.Status = "Published";
        await ctx.SaveChangesAsync();

        var svc = new HelpWorkflowDiagramService(ctx);
        var result = await svc.BuildAsync("TestModule", "TestCategory", "en");

        Assert.NotNull(result);
        Assert.Equal(2, result!.Nodes.Count);
        Assert.Equal(3, result.Edges.Count); // Start transition excluded, so 3 of the 4 seeded rows

        Assert.DoesNotContain(result.Edges, e => e.Label == "Start");

        var approveEdge = Assert.Single(result.Edges, e => e.FromNodeId == "Check" && e.ToNodeId == "Approval");
        Assert.Equal("normal", approveEdge.Kind);

        var rejectEdge = Assert.Single(result.Edges, e => e.Kind == "reject");
        Assert.Equal("Approval", rejectEdge.FromNodeId);
        Assert.Equal("Check", rejectEdge.ToNodeId);

        var terminalEdge = Assert.Single(result.Edges, e => e.ToNodeId == null);
        Assert.Equal("terminal", terminalEdge.Kind);
        Assert.Equal("Approval", terminalEdge.FromNodeId);
    }

    [Fact]
    public async Task BuildAsync_PersianLanguage_UsesVerifiedTranslationDictionary_FallsBackToRawNameIfUnknown()
    {
        var ctx = CreateContext(nameof(BuildAsync_PersianLanguage_UsesVerifiedTranslationDictionary_FallsBackToRawNameIfUnknown));
        // Phase 1 Hardening: نسخه‌ی Published تغییرناپذیر است؛ پس مراحل/گذارها را روی Draft می‌سازیم و بعد منتشر می‌کنیم
        // (همان ترتیب واقعی چرخه‌ی عمر). ادعاهای تست بدون تغییر است.
        var (_, version) = await SeedWorkflowAsync(ctx, "TestModule", "TestCategory", 1, "Draft");

        // "Check" یک نام واقعی‌تاییدشده در دیکشنری است؛ "TotallyMadeUpStageName" نیست
        var known = new WorkflowStage { WorkflowVersionId = version.WorkflowVersionId, Name = "Check", StageOrder = 0 };
        var unknown = new WorkflowStage { WorkflowVersionId = version.WorkflowVersionId, Name = "TotallyMadeUpStageName", StageOrder = 1 };
        ctx.WorkflowStages.AddRange(known, unknown);
        await ctx.SaveChangesAsync();

        version.Status = "Published";
        await ctx.SaveChangesAsync();

        var svc = new HelpWorkflowDiagramService(ctx);
        var result = await svc.BuildAsync("TestModule", "TestCategory", "fa");

        Assert.NotNull(result);
        var knownNode = result!.Nodes.Single(n => n.Id == "Check");
        var unknownNode = result.Nodes.Single(n => n.Id == "TotallyMadeUpStageName");

        Assert.NotEqual("Check", knownNode.Label); // باید ترجمه شده باشد
        Assert.Equal("TotallyMadeUpStageName", unknownNode.Label); // باید دست‌نخورده بماند - نه حدس
    }

    [Fact]
    public async Task GetAvailableWorkflowSourcesAsync_OnlyReturnsActiveDefinitions()
    {
        var ctx = CreateContext(nameof(GetAvailableWorkflowSourcesAsync_OnlyReturnsActiveDefinitions));
        ctx.WorkflowDefinitions.Add(new WorkflowDefinition { Name = "Active One", Module = "M1", Category = "C1", IsActive = true });
        ctx.WorkflowDefinitions.Add(new WorkflowDefinition { Name = "Inactive One", Module = "M2", Category = "C2", IsActive = false });
        await ctx.SaveChangesAsync();

        var svc = new HelpWorkflowDiagramService(ctx);
        var sources = await svc.GetAvailableWorkflowSourcesAsync();

        Assert.Contains(sources, s => s.Module == "M1");
        Assert.DoesNotContain(sources, s => s.Module == "M2");
    }
}
