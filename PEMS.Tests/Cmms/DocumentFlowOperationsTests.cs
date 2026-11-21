using System.Text.Json.Nodes;
using PEMS.Application.Definitions;
using PEMS.Domain.Entities;
using PEMS.Persistence.Seed;
using PEMS.Tests.TestSupport;
using PEMS.Web.Definitions;
using PEMS.Web.Services;

namespace PEMS.Tests.Cmms;

/// <summary>
/// SCRUM-684, 686, 732: the revision history, the document approval queue and the OCR queue are operations (the pages are definitions). This keeps what the controller tests proved: the deterministic order, the
/// status filter, the global counts, the document number without an include, the two waiting stages of the queue, the thirty rows of the history, and the permissions that guarded the old actions (now the floor
/// of the operation and the settings of the shipped actions).
/// </summary>
public class DocumentFlowOperationsTests
{
    private static int _nextId = 1000;

    private static (DocumentFlowOperations Ops, PEMS.Persistence.Context.PEMSDbContext Ctx) Create(string name)
    {
        var ctx = PemsDbContextFactory.CreateInMemory("DocumentFlow_" + name);
        return (new DocumentFlowOperations(ctx), ctx);
    }

    private static DocumentAttachment Attachment(string fileName, DateTime uploadDate, string status = "Pending") => new()
    {
        AttachmentId = _nextId++, FileName = fileName, FileExtension = ".pdf", FilePath = "/uploads/" + fileName, AIStatus = status, UploadDate = uploadDate, IsActive = true
    };

    private static EngineeringDocument Document(int id, string no, string status, DateTime created) => new()
    {
        DocumentId = id, DocumentNo = no, Title = "T-" + no, DocumentType = "Datasheet", ProjectId = 1, Status = status, IsActive = true, CreatedDate = created, ModifiedDate = created
    };

    // ------------------------------------------------------------ OCR queue
    [Fact]
    public async Task Queue_SameUploadDate_KeepsTheSameOrderTwice()
    {
        var (ops, ctx) = Create(nameof(Queue_SameUploadDate_KeepsTheSameOrderTwice));
        for (var i = 0; i < 10; i++) ctx.DocumentAttachments.Add(Attachment($"file{i}.pdf", DateTime.Today));
        await ctx.SaveChangesAsync();

        var first = await ops.QueueAsync();
        var second = await ops.QueueAsync();

        Assert.Equal(first.Select(x => x.AttachmentId), second.Select(x => x.AttachmentId));
        Assert.Equal(10, first.Count);
    }

    [Fact]
    public async Task Queue_StatusFilter_ShowsOnlyThatStatus()
    {
        var (ops, ctx) = Create(nameof(Queue_StatusFilter_ShowsOnlyThatStatus));
        ctx.DocumentAttachments.Add(Attachment("a.pdf", DateTime.Today, "Completed"));
        ctx.DocumentAttachments.Add(Attachment("b.pdf", DateTime.Today, "Failed"));
        await ctx.SaveChangesAsync();

        var rows = await ops.QueueAsync("Completed");

        Assert.Equal("a.pdf", Assert.Single(rows).FileName);
    }

    [Fact]
    public async Task Counts_AreGlobal_AndMissingStatusesAreZero()
    {
        var (ops, ctx) = Create(nameof(Counts_AreGlobal_AndMissingStatusesAreZero));
        ctx.DocumentAttachments.Add(Attachment("a.pdf", DateTime.Today, "Completed"));
        ctx.DocumentAttachments.Add(Attachment("b.pdf", DateTime.Today, "Completed"));
        ctx.DocumentAttachments.Add(Attachment("c.pdf", DateTime.Today, "Failed"));
        await ctx.SaveChangesAsync();

        var counts = await ops.CountsAsync();

        Assert.Equal(new PEMS.Application.Interfaces.OcrCounts(0, 0, 2, 1), counts);
    }

    [Fact]
    public async Task Queue_DocumentNo_IsFilledWithoutAnInclude()
    {
        var (ops, ctx) = Create(nameof(Queue_DocumentNo_IsFilledWithoutAnInclude));
        ctx.EngineeringDocuments.Add(Document(1, "DOC-001", "Draft", DateTime.Today));
        var attachment = Attachment("a.pdf", DateTime.Today); attachment.DocumentId = 1;
        ctx.DocumentAttachments.Add(attachment);
        await ctx.SaveChangesAsync();

        Assert.Equal("DOC-001", Assert.Single(await ops.QueueAsync()).DocumentNo);
    }

    // ------------------------------------------------------------ approval queue
    [Fact]
    public async Task Pending_WithoutAFilter_ShowsTheSubmittedAndTheCheckedStage_OldestFirst()
    {
        var (ops, ctx) = Create(nameof(Pending_WithoutAFilter_ShowsTheSubmittedAndTheCheckedStage_OldestFirst));
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "P1", ProjectName = "Project One", IsActive = true });   // the project is a required relation of a document
        ctx.EngineeringDocuments.AddRange(Document(1, "D-1", "Submitted", DateTime.Today.AddDays(-1)), Document(2, "D-2", "Checked", DateTime.Today.AddDays(-3)), Document(3, "D-3", "Approved", DateTime.Today.AddDays(-5)));
        await ctx.SaveChangesAsync();

        var rows = await ops.PendingAsync();

        Assert.Equal(["D-2", "D-1"], rows.Select(r => r.DocumentNo));
        Assert.Equal("D-3", Assert.Single(await ops.PendingAsync("Approved")).DocumentNo);
    }

    [Fact]
    public async Task History_IsTheThirtyMostRecentRows()
    {
        var (ops, ctx) = Create(nameof(History_IsTheThirtyMostRecentRows));
        ctx.EngineeringDocuments.Add(Document(1, "D-1", "Checked", DateTime.Today));
        for (var i = 0; i < 35; i++)
            ctx.DocumentWorkflows.Add(new DocumentWorkflow { DocumentId = 1, FromStatus = "Submitted", ToStatus = "Checked", ActionType = "Check", ActionDate = DateTime.Today.AddMinutes(i) });
        await ctx.SaveChangesAsync();

        var rows = await ops.HistoryAsync();

        Assert.Equal(30, rows.Count);
        Assert.Equal(DateTime.Today.AddMinutes(34), rows[0].ActionDate);
        Assert.All(rows, r => Assert.Equal("—", r.ActionBy));
    }

    // ------------------------------------------------------------ revisions
    [Fact]
    public async Task Revisions_AreFilteredByTheDocument_NewestFirst()
    {
        var (ops, ctx) = Create(nameof(Revisions_AreFilteredByTheDocument_NewestFirst));
        ctx.EngineeringDocuments.AddRange(Document(1, "D-1", "Draft", DateTime.Today), Document(2, "D-2", "Draft", DateTime.Today));
        ctx.DocumentRevisions.AddRange(
            new DocumentRevision { DocumentId = 1, RevisionNo = "00", RevisionDate = DateTime.Today.AddDays(-2), IsActive = true },
            new DocumentRevision { DocumentId = 1, RevisionNo = "01", RevisionDate = DateTime.Today.AddDays(-1), IsActive = true },
            new DocumentRevision { DocumentId = 2, RevisionNo = "00", RevisionDate = DateTime.Today, IsActive = true });
        await ctx.SaveChangesAsync();

        var all = await ops.RevisionsAsync();
        var one = await ops.RevisionsAsync(1);

        Assert.Equal(3, all.Count);
        Assert.Equal(["01", "00"], one.Select(r => r.RevisionNo));
        Assert.All(one, r => Assert.Equal("D-1", r.DocumentNo));
    }

    // ------------------------------------------------------------ the permissions of the old actions
    private static readonly OperationCatalog Catalog = OperationCatalog.Scan(typeof(PEMS.Application.Common.PemsOperationAttribute).Assembly);

    private static JsonObject Shipped(string kind, string key)
        => (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, SystemDefinitionSeeder.FolderName, kind, "core", key + ".json")))!["payload"]!;

    [Theory]
    [InlineData("OcrPage", "Retry", "OCRProcessing.Create")]
    [InlineData("OcrPage", "Queue", "OCRProcessing.View")]
    [InlineData("OcrPage", "Counts", "OCRProcessing.View")]
    [InlineData("DocumentWorkflow", "Approve", "EngineeringDocuments.Approve")]
    public void TheOperationsKeepTheFloorOfTheOldController(string model, string action, string permission)
        => Assert.Equal(permission, Catalog.Get(model, action)!.Permission);

    [Fact]
    public void TheApproveActionKeepsTheSeparationOfDutiesGuardOfTheEngineeringDocuments()
    {
        var guard = (JsonObject)Shipped("action", "document-approve")["parameters"]!["guard"]!;

        Assert.Equal("EngineeringDocuments", (string?)guard["resource"]);
        Assert.Equal("Approve", (string?)guard["action"]);
        Assert.Equal("sod", (string?)guard["mode"]);
        Assert.Equal("field:id", (string?)guard["resourceId"]);
    }
}
