using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Workflow;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Definitions;

/// <summary>
/// SCRUM-804: the converted workflow seeders ship a document (Workflow/Shipped/*.workflow.json). Clean-install parity: the stored workflow has exactly the structure the C# seeder wrote (module, context, name,
/// stage, type, responsible roles, transitions, published version 1), and running the seeder again changes nothing.
/// </summary>
public sealed class ShippedWorkflowParityTests
{
    // document, seeder, module, context, name, stage, stage type, roles (null = from the rule at start), transitions (action list after Start)
    public static IEnumerable<object?[]> Families() =>
    [
        ["irn-hold-resolution", IrnHoldResolutionWorkflowSeeder.Module, IrnHoldResolutionWorkflowSeeder.Category, "Goods receipt - Hold resolution", IrnHoldResolutionWorkflowSeeder.StageName, "Sequential", RoleCatalog.WarehouseManager, "Complete,Cancel"],
        ["pre-procurement", PreProcurementWorkflowSeeder.Module, PreProcurementWorkflowSeeder.Category, "Pre-procurement review", PreProcurementWorkflowSeeder.StageName, "Sequential", string.Join(",", RoleCatalog.ProcurementBuyer, RoleCatalog.ProcurementManager), "Complete,Cancel"],
        ["purchase-order-approval", PurchaseOrderApprovalWorkflowSeeder.Module, PurchaseOrderApprovalWorkflowSeeder.Category, "Purchase order approval", PurchaseOrderApprovalWorkflowSeeder.StageName, "Parallel", null, "Approve,Reject"],
        ["warehouse-intake", WarehouseIntakeWorkflowSeeder.Module, WarehouseIntakeWorkflowSeeder.Category, "Warehouse intake of work-order materials", WarehouseIntakeWorkflowSeeder.StageName, "Sequential", string.Join(",", RoleCatalog.WarehouseManager, RoleCatalog.WarehouseStorekeeper), "Complete,Cancel"],
        ["irn-disposition", IrnDispositionWorkflowSeeder.Module, IrnDispositionWorkflowSeeder.Category, "تایید Disposition دریافت مادی (IRN)", "QADispositionApproval", "Sequential", string.Join(",", SystemRoles.SystemAdmin, SystemRoles.QAManager), "Approve,Reject"],
        ["maintenance-strategy", MaintenanceStrategyWorkflowSeeder.Module, MaintenanceStrategyWorkflowSeeder.Category, "تایید استراتژی نگهداری (RCM)", "StrategyReview", "Sequential", string.Join(",", SystemRoles.SystemAdmin, SystemRoles.EngineeringSupervisor, SystemRoles.EngineeringManager), "Approve,Reject"],
        ["ncr-critical-approval", NcrApprovalWorkflowSeeder.Module, NcrApprovalWorkflowSeeder.Category, "تایید NCR بحرانی", "QAApproval", "Sequential", string.Join(",", SystemRoles.SystemAdmin, SystemRoles.QAManager), "Approve,Reject"],
    ];

    private static (IServiceProvider Sp, PEMSDbContext Db) Setup()
    {
        var db = PemsDbContextFactory.CreateInMemory("ship-parity-" + Guid.NewGuid());
        return (new ServiceCollection().AddSingleton(db).AddLogging().BuildServiceProvider(), db);
    }

    private static Task Seed(string document, IServiceProvider sp) => ShippedWorkflowCatalog.ShipAsync(sp, document);

    [Fact]
    public void Every_shipped_document_is_one_of_the_converted_families()
        => Assert.Equal(Families().Select(f => (string)f[0]!).OrderBy(n => n, StringComparer.Ordinal), ShippedWorkflowCatalog.Names());

    [Theory]
    [MemberData(nameof(Families))]
    public async Task A_clean_install_stores_the_structure_the_seeder_wrote(string document, string module, string context, string name, string stage, string stageType, string? roles, string endActions)
    {
        var (sp, db) = Setup();
        await Seed(document, sp);

        var definition = await db.WorkflowDefinitions.SingleAsync();
        Assert.Equal((module, context, name, true), (definition.Module, definition.Category, definition.Name, definition.IsActive));
        var version = await db.WorkflowVersions.SingleAsync();
        Assert.Equal((1, "Published"), (version.VersionNumber, version.Status));

        var s = await db.WorkflowStages.SingleAsync();
        Assert.Equal((stage, 1, stageType, roles, "AllApprovalsRequired", true), (s.Name, s.StageOrder, s.StageType, s.ResponsibleRole, s.ApprovalStrategy, s.IsMandatory));

        var transitions = await db.WorkflowTransitions.ToListAsync();
        var actual = transitions.OrderBy(t => t.WorkflowTransitionId).Select(t => (t.FromStageId, t.ToStageId, t.ActionType, t.Priority)).ToList();
        var expected = new List<(int?, int?, string, int)> { (null, s.WorkflowStageId, "Start", 0) };
        expected.AddRange(endActions.Split(',').Select(a => ((int?)s.WorkflowStageId, (int?)null, a, 0)));
        Assert.Equal(expected, actual);
    }

    public static IEnumerable<object[]> Documents() => Families().Select(f => new object[] { f[0]! });

    [Theory]
    [MemberData(nameof(Documents))]
    public async Task Seeding_again_changes_nothing(string document)
    {
        var (sp, db) = Setup();
        await Seed(document, sp);
        await Seed(document, sp);
        Assert.Equal((1, 1, 1), (await db.WorkflowDefinitions.CountAsync(), await db.WorkflowVersions.CountAsync(), await db.WorkflowStages.CountAsync()));
    }

    [Fact]
    public async Task A_workflow_the_customer_renamed_is_left_alone()
    {
        var (sp, db) = Setup();
        await Seed("pre-procurement", sp);
        (await db.WorkflowDefinitions.SingleAsync()).Name = "Our buyer review";
        await db.SaveChangesAsync();

        await Seed("pre-procurement", sp);

        Assert.Equal(("Our buyer review", 1), ((await db.WorkflowDefinitions.SingleAsync()).Name, await db.WorkflowVersions.CountAsync()));
    }

    [Fact]
    public async Task An_install_made_by_the_old_seeder_with_the_same_structure_gets_no_review_draft()
    {
        var (sp, db) = Setup();
        await Seed("pre-procurement", sp);
        (await db.WorkflowVersions.SingleAsync()).ChangeSummary = null;   // the old seeder left no shipped marker
        await db.SaveChangesAsync();

        await Seed("pre-procurement", sp);

        Assert.Equal(1, await db.WorkflowVersions.CountAsync());
    }

    [Fact]
    public async Task A_changed_shipped_document_is_stored_as_an_upstream_draft_with_a_diff_and_the_active_version_stays()
    {
        var (sp, db) = Setup();
        await Seed("pre-procurement", sp);
        var changed = ShippedWorkflowCatalog.Load("pre-procurement");
        changed.Stages[0].ResponsibleRole = RoleCatalog.ProcurementManager;   // the next release ships another responsible role

        var result = await new WorkflowShipper(db, new PEMS.WorkflowPlatform.Services.WorkflowDefinitionService(db)).ShipAsync(changed, ShippedWorkflowCatalog.SystemActor);

        Assert.Equal(ShipOutcome.UpstreamStored, result.Outcome);
        Assert.Single(result.Changes);
        var versions = await db.WorkflowVersions.OrderBy(v => v.VersionNumber).ToListAsync();
        Assert.Equal(new[] { ("Published", 1), ("Draft", 2) }, versions.Select(v => (v.Status, v.VersionNumber)).ToArray());
        Assert.EndsWith(WorkflowShipper.UpstreamNote, versions[1].ChangeSummary);
    }
}
