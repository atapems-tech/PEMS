using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق «PEMS EDMS Phase 3 Transmittal Audit» یافته G-21/G-22: این تست‌ها دقیقاً همان سناریوی
/// اجباری بند ۱۹ سند (Historical Immutability Test) را بازتولید می‌کنند - رویژن A صادر می‌شود،
/// رویژن B ساخته می‌شود، و Transmittal همچنان باید هویت فایل رویژن A را نشان بدهد، هرگز B را.
/// </summary>
public class TransmittalHistoricalIntegrityTests
{
    private static async Task<(TransmittalService svc, Project project, PEMS.Persistence.Context.PEMSDbContext ctx)> SeedAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory(dbName);
        var project = new Project { ProjectName = "Test Project", ProjectCode = "TP" };
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        return (new TransmittalService(ctx), project, ctx);
    }

    private static List<TransmittalRecipientInput> Recipients(params string[] names) =>
        names.Select(n => new TransmittalRecipientInput { Name = n, RecipientType = "Client" }).ToList();

    /// <summary>
    /// سند D-001، رویژن A، فایل رسمی A.pdf (Hash A) - دقیقاً سناریوی بند ۱۹.
    /// </summary>
    private static async Task<(Document doc, DocumentRevision revA, DocumentAttachment fileA)> SeedDocumentWithRevisionAAsync(
        PEMS.Persistence.Context.PEMSDbContext ctx, int projectId)
    {
        var doc = new EngineeringDocument
        {
            DocumentNo = $"D-001-{Guid.NewGuid():N}"[..20],
            Title = "Test Doc",
            DocumentType = "Datasheet",
            ProjectId = projectId,
            Status = "Draft",
            RevisionNo = "A"
        };
        ctx.EngineeringDocuments.Add(doc);
        await ctx.SaveChangesAsync();

        var revA = new DocumentRevision { DocumentId = doc.DocumentId, RevisionNo = "A", Status = "Approved", IsActive = true };
        ctx.DocumentRevisions.Add(revA);
        await ctx.SaveChangesAsync();

        var fileA = new DocumentAttachment
        {
            DocumentId = doc.DocumentId,
            DocumentRevisionId = revA.RevisionId,
            IsOfficial = true,
            IsActive = true,
            FileName = "A.pdf",
            FileExtension = ".pdf",
            FilePath = "documents/a.pdf",
            FileSize = 1024,
            FileHash = "HASH-A-AAAAAAAAAAAAAAAA",
            UploadDate = DateTime.Now
        };
        ctx.DocumentAttachments.Add(fileA);
        await ctx.SaveChangesAsync();

        return (doc, revA, fileA);
    }

    [Fact]
    public async Task MandatoryScenario_IssueRevisionA_ThenCreateRevisionB_TransmittalStillShowsRevisionAFileIdentity()
    {
        var (svc, project, ctx) = await SeedAsync(nameof(MandatoryScenario_IssueRevisionA_ThenCreateRevisionB_TransmittalStillShowsRevisionAFileIdentity));
        var (doc, revA, fileA) = await SeedDocumentWithRevisionAAsync(ctx, project.ProjectId);

        var id = await svc.CreateAsync("T-001", project.ProjectId, "Eng", "Client", "IFA", null,
            new List<int> { doc.DocumentId }, false, Recipients("Client DCC"));

        await svc.MarkSentAsync(id);

        // اکنون رویژن B ساخته می‌شود - دقیقاً سناریوی بند ۱۹
        var revB = new DocumentRevision { DocumentId = doc.DocumentId, RevisionNo = "B", Status = "Draft", IsActive = true };
        ctx.DocumentRevisions.Add(revB);
        doc.RevisionNo = "B";
        await ctx.SaveChangesAsync();

        var fileB = new DocumentAttachment
        {
            DocumentId = doc.DocumentId,
            DocumentRevisionId = revB.RevisionId,
            IsOfficial = true,
            IsActive = true,
            FileName = "B.pdf",
            FileExtension = ".pdf",
            FilePath = "documents/b.pdf",
            FileSize = 2048,
            FileHash = "HASH-B-BBBBBBBBBBBBBBBB",
            UploadDate = DateTime.Now
        };
        ctx.DocumentAttachments.Add(fileB);
        await ctx.SaveChangesAsync();

        // T-001 هرگز نباید به‌صورت پویا به رویژن B تغییر کند
        var dto = await svc.GetByIdAsync(id);
        var item = dto!.Items.Single();

        Assert.Equal("A", item.RevisionNo);
        Assert.Equal(revA.RevisionId, item.DocumentRevisionId);
        Assert.Equal("A.pdf", item.SnapshotFileName);
        Assert.Equal("HASH-A-AAAAAAAAAAAAAAAA", item.SnapshotFileHash);
        Assert.Equal(1024, item.SnapshotFileSize);

        Assert.NotEqual(revB.RevisionId, item.DocumentRevisionId);
        Assert.NotEqual("B.pdf", item.SnapshotFileName);
        Assert.NotEqual("HASH-B-BBBBBBBBBBBBBBBB", item.SnapshotFileHash);
    }

    [Fact]
    public async Task CreateAsync_DocumentWithNoRevisionRecord_LeavesSnapshotFieldsNull_NotGuessed()
    {
        var (svc, project, ctx) = await SeedAsync(nameof(CreateAsync_DocumentWithNoRevisionRecord_LeavesSnapshotFieldsNull_NotGuessed));

        var doc = new EngineeringDocument
        {
            DocumentNo = $"D-NOREV-{Guid.NewGuid():N}"[..20],
            Title = "No DocumentRevision row",
            DocumentType = "Datasheet",
            ProjectId = project.ProjectId,
            Status = "Draft",
            RevisionNo = "00"
        };
        ctx.EngineeringDocuments.Add(doc);
        await ctx.SaveChangesAsync();

        var id = await svc.CreateAsync("T-002", project.ProjectId, "Eng", "Client", "IFA", null,
            new List<int> { doc.DocumentId }, false, Recipients("Client DCC"));

        var dto = await svc.GetByIdAsync(id);
        var item = dto!.Items.Single();

        Assert.Equal("00", item.RevisionNo);
        Assert.Null(item.DocumentRevisionId);
        Assert.Null(item.SnapshotFileName);
        Assert.Null(item.SnapshotFileHash);
    }

    [Fact]
    public async Task ReissueAsync_CopiesSnapshotFieldsExactly_DoesNotReResolve()
    {
        var (svc, project, ctx) = await SeedAsync(nameof(ReissueAsync_CopiesSnapshotFieldsExactly_DoesNotReResolve));
        var (doc, revA, fileA) = await SeedDocumentWithRevisionAAsync(ctx, project.ProjectId);

        var id = await svc.CreateAsync("T-003", project.ProjectId, "Eng", "Client", "IFA", null,
            new List<int> { doc.DocumentId }, false, Recipients("Client DCC"));
        await svc.MarkSentAsync(id);
        await svc.CancelAsync(id, "test cancel", null);

        // بعد از Cancel، رویژن B ساخته می‌شود - Reissue نباید خودکار به آن اشاره کند
        var revB = new DocumentRevision { DocumentId = doc.DocumentId, RevisionNo = "B", Status = "Draft", IsActive = true };
        ctx.DocumentRevisions.Add(revB);
        doc.RevisionNo = "B";
        await ctx.SaveChangesAsync();

        var newId = await svc.ReissueAsync(id, null);
        var dto = await svc.GetByIdAsync(newId);
        var item = dto!.Items.Single();

        Assert.Equal(revA.RevisionId, item.DocumentRevisionId);
        Assert.Equal("A.pdf", item.SnapshotFileName);
        Assert.Equal("HASH-A-AAAAAAAAAAAAAAAA", item.SnapshotFileHash);
    }

    [Fact]
    public async Task AcknowledgeAsync_SingleRecipient_RecordsAcknowledgementButLeavesTransmittalStatusSent()
    {
        var (svc, project, ctx) = await SeedAsync(nameof(AcknowledgeAsync_SingleRecipient_RecordsAcknowledgementButLeavesTransmittalStatusSent));
        var (doc, _, _) = await SeedDocumentWithRevisionAAsync(ctx, project.ProjectId);

        var id = await svc.CreateAsync("T-004", project.ProjectId, "Eng", "Client", "IFA", null,
            new List<int> { doc.DocumentId }, false, Recipients("Client DCC", "Contractor DCC"));
        await svc.MarkSentAsync(id);

        var dto = await svc.GetByIdAsync(id);
        var recipient1 = dto!.Recipients[0];

        await svc.AcknowledgeAsync(recipient1.TransmittalRecipientId, "received, thanks", 1);

        var reloaded = await svc.GetByIdAsync(id);
        var reloadedR1 = reloaded!.Recipients.Single(r => r.TransmittalRecipientId == recipient1.TransmittalRecipientId);

        Assert.NotNull(reloadedR1.AcknowledgedAt);
        Assert.Equal("received, thanks", reloadedR1.AcknowledgementNote);

        // بند ۱۵: چون گیرنده‌ی دوم هنوز تایید نکرده، خودِ Transmittal نباید Acknowledged شود
        Assert.Equal("Sent", reloaded.Status);
        Assert.False(reloaded.Recipients.Single(r => r.TransmittalRecipientId != recipient1.TransmittalRecipientId).AcknowledgedAt.HasValue);
    }

    [Fact]
    public async Task AcknowledgeAsync_AllRecipientsAcknowledge_TransitionsTransmittalToAcknowledged()
    {
        var (svc, project, ctx) = await SeedAsync(nameof(AcknowledgeAsync_AllRecipientsAcknowledge_TransitionsTransmittalToAcknowledged));
        var (doc, _, _) = await SeedDocumentWithRevisionAAsync(ctx, project.ProjectId);

        var id = await svc.CreateAsync("T-005", project.ProjectId, "Eng", "Client", "IFA", null,
            new List<int> { doc.DocumentId }, false, Recipients("Client DCC", "Contractor DCC"));
        await svc.MarkSentAsync(id);

        var dto = await svc.GetByIdAsync(id);
        foreach (var r in dto!.Recipients)
            await svc.AcknowledgeAsync(r.TransmittalRecipientId, null, 1);

        var reloaded = await svc.GetByIdAsync(id);
        Assert.Equal("Acknowledged", reloaded!.Status);
        Assert.All(reloaded.Recipients, r => Assert.True(r.AcknowledgedAt.HasValue));
    }

    [Fact]
    public async Task AcknowledgeAsync_DuplicateAcknowledgement_Throws()
    {
        var (svc, project, ctx) = await SeedAsync(nameof(AcknowledgeAsync_DuplicateAcknowledgement_Throws));
        var (doc, _, _) = await SeedDocumentWithRevisionAAsync(ctx, project.ProjectId);

        var id = await svc.CreateAsync("T-006", project.ProjectId, "Eng", "Client", "IFA", null,
            new List<int> { doc.DocumentId }, false, Recipients("Client DCC"));
        await svc.MarkSentAsync(id);

        var dto = await svc.GetByIdAsync(id);
        var recipientId = dto!.Recipients.Single().TransmittalRecipientId;

        await svc.AcknowledgeAsync(recipientId, null, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AcknowledgeAsync(recipientId, null, 1));
    }

    [Fact]
    public async Task AcknowledgeAsync_TransmittalStillDraft_Throws()
    {
        var (svc, project, ctx) = await SeedAsync(nameof(AcknowledgeAsync_TransmittalStillDraft_Throws));
        var (doc, _, _) = await SeedDocumentWithRevisionAAsync(ctx, project.ProjectId);

        var id = await svc.CreateAsync("T-007", project.ProjectId, "Eng", "Client", "IFA", null,
            new List<int> { doc.DocumentId }, false, Recipients("Client DCC"));

        var dto = await svc.GetByIdAsync(id);
        var recipientId = dto!.Recipients.Single().TransmittalRecipientId;

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AcknowledgeAsync(recipientId, null, 1));
    }
}
