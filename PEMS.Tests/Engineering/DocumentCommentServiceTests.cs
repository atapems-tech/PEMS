using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق Phase E-02 (بخش Comments از سند Remediation Directive، §16-17/§55): چرخه‌ی کامنت باید
/// قابل ردیابی باشد - کامنت اصلی هرگز رونویسی نشود، پاسخ/بستن در فیلدهای جدا ثبت شوند.
/// </summary>
public class DocumentCommentServiceTests
{
    private static (PEMS.Persistence.Context.PEMSDbContext ctx, DocumentCommentService service, int documentId) Seed(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory(dbName);
        var doc = new PEMS.Domain.Entities.EngineeringDocument
        {
            DocumentNo = "TP-MECH-DAT-0001",
            Title = "Test",
            DocumentType = "Datasheet",
            ProjectId = 1,
            Status = "Draft"
        };
        ctx.EngineeringDocuments.Add(doc);
        ctx.SaveChanges();

        return (ctx, new DocumentCommentService(ctx), doc.DocumentId);
    }

    [Fact]
    public async Task AddCommentAsync_CreatesComment_WithCategoryAndDueDate()
    {
        var (ctx, service, documentId) = Seed(nameof(AddCommentAsync_CreatesComment_WithCategoryAndDueDate));

        var due = DateTime.Now.AddDays(7);
        var commentId = await service.AddCommentAsync(documentId, "Please clarify operating pressure", "Reviewer A",
            category: "Technical", severity: "Major", requiredAction: "Update datasheet", dueDate: due);

        var comment = await ctx.DocumentComments.FindAsync(commentId);
        Assert.NotNull(comment);
        Assert.Equal("Please clarify operating pressure", comment!.CommentText);
        Assert.Equal("Technical", comment.Category);
        Assert.Equal("Major", comment.Severity);
        Assert.Equal(due, comment.DueDate);
    }

    [Fact]
    public async Task RecordResponseAsync_SetsResponse_WithoutOverwritingOriginalComment()
    {
        var (ctx, service, documentId) = Seed(nameof(RecordResponseAsync_SetsResponse_WithoutOverwritingOriginalComment));
        var commentId = await service.AddCommentAsync(documentId, "Update operating pressure", "Process Reviewer");

        await service.RecordResponseAsync(commentId, "Updated in Rev 03");

        var comment = await ctx.DocumentComments.FindAsync(commentId);
        Assert.Equal("Update operating pressure", comment!.CommentText); // اصل کامنت دست‌نخورده
        Assert.Equal("Updated in Rev 03", comment.ResponseText);
        Assert.NotNull(comment.ResponseDate);
    }

    [Fact]
    public async Task SetActionStatusAsync_UpdatesStatus_ThroughFullLifecycle()
    {
        var (ctx, service, documentId) = Seed(nameof(SetActionStatusAsync_UpdatesStatus_ThroughFullLifecycle));
        var commentId = await service.AddCommentAsync(documentId, "Check tolerance", "QA");

        await service.SetActionStatusAsync(commentId, "OpenComment");
        Assert.Equal("OpenComment", (await ctx.DocumentComments.FindAsync(commentId))!.ActionStatus);

        await service.RecordResponseAsync(commentId, "Fixed");
        await service.SetActionStatusAsync(commentId, "Verified");
        Assert.Equal("Verified", (await ctx.DocumentComments.FindAsync(commentId))!.ActionStatus);

        await service.SetActionStatusAsync(commentId, "Closed");
        Assert.Equal("Closed", (await ctx.DocumentComments.FindAsync(commentId))!.ActionStatus);
    }

    [Fact]
    public async Task GetForDocumentAsync_ReturnsOnlyCommentsForThatDocument()
    {
        var (ctx, service, documentId) = Seed(nameof(GetForDocumentAsync_ReturnsOnlyCommentsForThatDocument));
        var otherDoc = new PEMS.Domain.Entities.EngineeringDocument
        {
            DocumentNo = "TP-MECH-DAT-0002", Title = "Other", DocumentType = "Datasheet", ProjectId = 1, Status = "Draft"
        };
        ctx.EngineeringDocuments.Add(otherDoc);
        ctx.SaveChanges();

        await service.AddCommentAsync(documentId, "Comment for doc 1", "A");
        await service.AddCommentAsync(otherDoc.DocumentId, "Comment for doc 2", "B");

        var results = await service.GetForDocumentAsync(documentId);

        Assert.Single(results);
        Assert.Equal("Comment for doc 1", results[0].CommentText);
    }
}
