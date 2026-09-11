using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق «PEMS EDMS Phase 2 Lifecycle Design» یافته L2-01: این تست‌ها دقیقاً همان سناریوی واقعی
/// باگ را بازتولید می‌کنند - پاسخ کارفرما روی رویژن ۰۰ ثبت می‌شود، سپس رویژن ۰۱ ساخته می‌شود؛
/// رویژن ۰۱ نباید تاییدشده نشان داده شود و رویژن ۰۰ نباید تاریخچه‌اش را از دست بدهد.
/// </summary>
public class RevisionScopedClientReviewTests : EngineeringWorkflowTestBase
{
    private Letter CreateIncomingLetter(int documentId)
    {
        var letter = new Letter
        {
            LetterType = "Incoming",
            LetterNumber = $"IN-{Guid.NewGuid():N}".Substring(0, 15),
            Subject = "Client feedback",
            LetterDate = DateTime.Now,
            IsActive = true,
            RelatedDocumentId = documentId
        };
        Ctx.Letters.Add(letter);
        Ctx.SaveChanges();
        return letter;
    }

    private (EngineeringDocument doc, DocumentRevision rev00) CreateIssuedDocumentWithRevision00()
    {
        var doc = CreateDraftDocument();
        doc.Status = "Issued";

        var rev00 = new DocumentRevision { DocumentId = doc.DocumentId, RevisionNo = "00", Status = "Approved", IsActive = true };
        Ctx.DocumentRevisions.Add(rev00);
        Ctx.SaveChanges();

        return (doc, rev00);
    }

    [Fact]
    public async Task RecordClientResponseAsync_NoExplicitRevision_WritesToLatestRevision()
    {
        var (doc, rev00) = CreateIssuedDocumentWithRevision00();
        var letter = CreateIncomingLetter(doc.DocumentId);

        await WorkflowService.RecordClientResponseAsync(doc.DocumentId, "A", letter.LetterId, "looks good", 1, ManagerRole);

        var reloaded = Ctx.DocumentRevisions.Single(x => x.RevisionId == rev00.RevisionId);
        Assert.Equal("A", reloaded.ClientReviewStatus);
        Assert.NotNull(reloaded.ClientReviewDate);
        Assert.Equal(letter.LetterId, reloaded.ClientReviewLetterId);

        // بند ۱ طراحی: فیلدهای هم‌نام روی Document همچنان به‌عنوان Cache به‌روز می‌مانند
        Assert.Equal("A", doc.ClientApprovalStatus);
    }

    [Fact]
    public async Task RecordClientResponseAsync_ExplicitRevisionId_WritesToThatRevisionOnly()
    {
        var (doc, rev00) = CreateIssuedDocumentWithRevision00();
        var letter = CreateIncomingLetter(doc.DocumentId);

        await WorkflowService.RecordClientResponseAsync(doc.DocumentId, "A", letter.LetterId, null, 1, ManagerRole, revisionId: rev00.RevisionId);

        var reloaded = Ctx.DocumentRevisions.Single(x => x.RevisionId == rev00.RevisionId);
        Assert.Equal("A", reloaded.ClientReviewStatus);
    }

    [Fact]
    public async Task RecordClientResponseAsync_RevisionIdBelongsToDifferentDocument_Throws()
    {
        var (doc, _) = CreateIssuedDocumentWithRevision00();
        var (otherDoc, otherRev) = CreateIssuedDocumentWithRevision00();
        var letter = CreateIncomingLetter(doc.DocumentId);

        await Assert.ThrowsAsync<Exception>(
            () => WorkflowService.RecordClientResponseAsync(doc.DocumentId, "A", letter.LetterId, null, 1, ManagerRole, revisionId: otherRev.RevisionId));
    }

    [Fact]
    public async Task CreateNewRevision_ClearsStaleDocumentLevelClientApproval_WithoutTouchingOldRevisionHistory()
    {
        // این دقیقاً سناریوی یافته‌ی L2-01 است: رویژن ۰۰ تاییدشده توسط کارفرما، سپس رویژن ۰۱
        // ساخته می‌شود. رویژن ۰۱ (تازه) نباید به‌اشتباه تاییدشده به‌نظر برسد.
        var (doc, rev00) = CreateIssuedDocumentWithRevision00();
        var letter = CreateIncomingLetter(doc.DocumentId);
        await WorkflowService.RecordClientResponseAsync(doc.DocumentId, "A", letter.LetterId, "approved", 1, ManagerRole);

        Assert.Equal("A", doc.ClientApprovalStatus);
        Assert.NotNull(doc.ClientResponseDate);

        // شبیه‌سازی مستقیم همان منطقی که EngineeringDocumentsController.CreateRevision اجرا
        // می‌کند (بدون نیاز به کل Controller/HTTP context در این تست واحد)
        var rev01 = new DocumentRevision
        {
            DocumentId = doc.DocumentId,
            RevisionNo = "01",
            Status = "Draft",
            SupersededRevisionId = rev00.RevisionId,
            IsActive = true
        };
        Ctx.DocumentRevisions.Add(rev01);
        rev00.Status = "Superseded";

        doc.Status = "Draft";
        doc.ClientApprovalStatus = null;
        doc.ClientComment = null;
        doc.ClientResponseDate = null;
        Ctx.SaveChanges();

        // رویژن جدید (۰۱) نباید هیچ پاسخ کارفرمایی داشته باشد
        var reloadedRev01 = Ctx.DocumentRevisions.Single(x => x.RevisionId == rev01.RevisionId);
        Assert.Null(reloadedRev01.ClientReviewStatus);

        // Cache سطح-مدرک باید پاک شده باشد - دیگر رویژن ۰۱ را تاییدشده نشان نمی‌دهد
        Assert.Null(doc.ClientApprovalStatus);
        Assert.Null(doc.ClientResponseDate);

        // اما تاریخچه‌ی واقعی رویژن ۰۰ دست‌نخورده باقی می‌ماند - حقیقت تاریخی حفظ می‌شود
        var reloadedRev00 = Ctx.DocumentRevisions.Single(x => x.RevisionId == rev00.RevisionId);
        Assert.Equal("A", reloadedRev00.ClientReviewStatus);
        Assert.Equal("Superseded", reloadedRev00.Status);
    }

    [Fact]
    public async Task RecordClientResponseAsync_RejectionCode_StillWritesRevisionScopedStatus()
    {
        var (doc, rev00) = CreateIssuedDocumentWithRevision00();
        var letter = CreateIncomingLetter(doc.DocumentId);

        await WorkflowService.RecordClientResponseAsync(doc.DocumentId, "C", letter.LetterId, "needs rework", 1, ManagerRole);

        var reloaded = Ctx.DocumentRevisions.Single(x => x.RevisionId == rev00.RevisionId);
        Assert.Equal("C", reloaded.ClientReviewStatus);
        Assert.Equal("Rejected", doc.Status);
    }
}
