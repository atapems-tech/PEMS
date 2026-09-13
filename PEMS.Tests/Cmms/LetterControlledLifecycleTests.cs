using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Letter Details — Controlled Lifecycle» بند ۳۶: پوشش تست Edit/Workflow/
/// Authorization/Audit برای چرخه‌ی عمر کنترل‌شده‌ی نامه‌های Outgoing (Draft → PendingApproval →
/// Approved/ReturnedForRevision/Rejected → Issued → Closed) و قانون خودتاییدی
/// (CheckOriginalAuthorCanEdit). طبق قرارداد موجود این فایل‌ها (LetterGridPaginationTests):
/// PemsDbContextFactory.CreateInMemory + نمونه‌سازی واقعی LetterService (نه Mock).
/// </summary>
public class LetterControlledLifecycleTests
{
    private const int AuthorUserId = 100;
    private const int OtherUserId = 200;
    private const int ReviewerUserId = 300;

    private static LetterService CreateService(string dbName, out PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"LetterLifecycle_{dbName}");
        // طبق نمونه‌ی LetterGridPaginationTests: sanitizer واقعی (بدون وابستگی خارجی) - نه null -
        // چون SaveDraftEditAsync/ResubmitAsync/CreateAsync واقعاً آن را صدا می‌زنند
        return new LetterService(ctx, new RichTextSanitizerService(), knowledgeBaseService: null!);
    }

    private static Letter NewOutgoingDraft(int letterId, int? registeredById = AuthorUserId) => new()
    {
        LetterId = letterId,
        LetterType = "Outgoing",
        LetterNumber = $"OUT-{letterId:D4}",
        LetterDate = DateTime.Today,
        Receiver = "Vendor XYZ",
        Subject = "Original Subject",
        Body = "<p>Original body</p>",
        ConfidentialityLevel = "Internal",
        Status = "Draft",
        RegisteredById = registeredById,
        IsActive = true,
        CreatedDate = DateTime.Now
    };

    // ==============================================================================
    // Edit Tests
    // ==============================================================================

    [Fact]
    public async Task SaveDraftEditAsync_DraftByOwner_Succeeds()
    {
        var service = CreateService(nameof(SaveDraftEditAsync_DraftByOwner_Succeeds), out var ctx);
        ctx.Letters.Add(NewOutgoingDraft(1));
        await ctx.SaveChangesAsync();

        var (success, error) = await service.SaveDraftEditAsync(1, "New Subject", "<p>New body</p>", AuthorUserId, null);

        Assert.True(success, error);
        var letter = await ctx.Letters.FindAsync(1);
        Assert.Equal("New Subject", letter!.Subject);
        Assert.Contains("New body", letter.Body);
        Assert.Equal("Draft", letter.Status);
    }

    [Fact]
    public async Task SaveDraftEditAsync_NonOwnerOnDraft_Rejected()
    {
        var service = CreateService(nameof(SaveDraftEditAsync_NonOwnerOnDraft_Rejected), out var ctx);
        ctx.Letters.Add(NewOutgoingDraft(1));
        await ctx.SaveChangesAsync();

        var (success, error) = await service.SaveDraftEditAsync(1, "Hacked Subject", "hacked", OtherUserId, null);

        Assert.False(success);
        Assert.NotNull(error);
        var letter = await ctx.Letters.FindAsync(1);
        Assert.Equal("Original Subject", letter!.Subject);
    }

    [Theory]
    [InlineData("PendingApproval")]
    [InlineData("Approved")]
    [InlineData("Issued")]
    [InlineData("Rejected")]
    [InlineData("Closed")]
    public async Task SaveDraftEditAsync_NonEditableStatus_RejectedEvenForOwner(string status)
    {
        // طبق بند ۲: هرکدام از این وضعیت‌ها یعنی محتوای رسمی «خارج از Draft» شده - باید در سطح
        // سرویس رد شود، حتی اگر یک POST مستقیم (بدون UI) ساخته شود و حتی توسط خودِ نویسنده‌ی اصلی
        var service = CreateService($"{nameof(SaveDraftEditAsync_NonEditableStatus_RejectedEvenForOwner)}_{status}", out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = status;
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.SaveDraftEditAsync(1, "Bypassed Subject", "bypassed", AuthorUserId, null);

        Assert.False(success);
        Assert.NotNull(error);
        var reloaded = await ctx.Letters.FindAsync(1);
        Assert.Equal("Original Subject", reloaded!.Subject);
    }

    [Fact]
    public async Task SaveDraftEditAsync_ReturnedForRevisionByOriginalAuthor_Succeeds()
    {
        var service = CreateService(nameof(SaveDraftEditAsync_ReturnedForRevisionByOriginalAuthor_Succeeds), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = "ReturnedForRevision";
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.SaveDraftEditAsync(1, "Fixed Subject", "<p>Fixed body</p>", AuthorUserId, null);

        Assert.True(success, error);
    }

    [Fact]
    public async Task SaveDraftEditAsync_ReturnedForRevisionByNonAuthor_Rejected()
    {
        // طبق بند ۷: قانون فقط بر اساس RegisteredById است - نه Role. حتی یک بازبین/Reviewer هم
        // اگر نویسنده‌ی اصلی نباشد، حق ویرایش محتوا را ندارد
        var service = CreateService(nameof(SaveDraftEditAsync_ReturnedForRevisionByNonAuthor_Rejected), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = "ReturnedForRevision";
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.SaveDraftEditAsync(1, "Hijacked", "hijacked", ReviewerUserId, null);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task ResubmitAsync_ThenSaveDraftEditAsync_ReLocksEditing()
    {
        // طبق بند ۱۰/۳۵: بلافاصله بعد از بازارسال، دوباره غیرقابل‌ویرایش می‌شود
        var service = CreateService(nameof(ResubmitAsync_ThenSaveDraftEditAsync_ReLocksEditing), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = "ReturnedForRevision";
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (resubmitSuccess, resubmitError) = await service.ResubmitAsync(1, "Resubmitted Subject", "<p>Resubmitted</p>", AuthorUserId, null);
        Assert.True(resubmitSuccess, resubmitError);

        var (editSuccess, editError) = await service.SaveDraftEditAsync(1, "Should Fail", "should fail", AuthorUserId, null);

        Assert.False(editSuccess);
        Assert.NotNull(editError);
        var reloaded = await ctx.Letters.FindAsync(1);
        Assert.Equal("PendingApproval", reloaded!.Status);
        Assert.Equal("Resubmitted Subject", reloaded.Subject);
    }

    // ==============================================================================
    // Workflow Tests (گذارهای مجاز/غیرمجاز)
    // ==============================================================================

    [Fact]
    public async Task SubmitForReviewAsync_FromDraft_MovesToPendingApproval()
    {
        var service = CreateService(nameof(SubmitForReviewAsync_FromDraft_MovesToPendingApproval), out var ctx);
        ctx.Letters.Add(NewOutgoingDraft(1));
        await ctx.SaveChangesAsync();

        var (success, error) = await service.SubmitForReviewAsync(1, AuthorUserId);

        Assert.True(success, error);
        Assert.Equal("PendingApproval", (await ctx.Letters.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task SubmitForReviewAsync_ByNonOwner_Rejected()
    {
        var service = CreateService(nameof(SubmitForReviewAsync_ByNonOwner_Rejected), out var ctx);
        ctx.Letters.Add(NewOutgoingDraft(1));
        await ctx.SaveChangesAsync();

        var (success, error) = await service.SubmitForReviewAsync(1, OtherUserId);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal("Draft", (await ctx.Letters.FindAsync(1))!.Status);
    }

    [Theory]
    [InlineData("PendingApproval")]
    [InlineData("Approved")]
    [InlineData("Issued")]
    public async Task SubmitForReviewAsync_FromNonDraftStatus_Rejected(string currentStatus)
    {
        var service = CreateService($"{nameof(SubmitForReviewAsync_FromNonDraftStatus_Rejected)}_{currentStatus}", out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = currentStatus;
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.SubmitForReviewAsync(1, AuthorUserId);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task ApproveAsync_FromPendingApproval_MovesToApproved_AndRecordsReview()
    {
        var service = CreateService(nameof(ApproveAsync_FromPendingApproval_MovesToApproved_AndRecordsReview), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = "PendingApproval";
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.ApproveAsync(1, ReviewerUserId, "Looks good");

        Assert.True(success, error);
        var reloaded = await ctx.Letters.FindAsync(1);
        Assert.Equal("Approved", reloaded!.Status);
        Assert.Equal(ReviewerUserId, reloaded.ApprovedById);
        Assert.NotNull(reloaded.ApprovedDate);

        var review = Assert.Single(ctx.LetterReviews);
        Assert.Equal(LetterReviewDecisions.Approved, review.Decision);
        Assert.Equal(ReviewerUserId, review.ReviewerUserId);
        Assert.Equal("Looks good", review.Comment);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Approved")]
    [InlineData("Issued")]
    [InlineData("ReturnedForRevision")]
    public async Task ApproveAsync_FromNonPendingApprovalStatus_Rejected(string currentStatus)
    {
        var service = CreateService($"{nameof(ApproveAsync_FromNonPendingApprovalStatus_Rejected)}_{currentStatus}", out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = currentStatus;
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.ApproveAsync(1, ReviewerUserId, null);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Empty(ctx.LetterReviews);
    }

    [Fact]
    public async Task ReturnForRevisionAsync_FromPendingApproval_MovesToReturnedForRevision_AndSnapshotsContent()
    {
        var service = CreateService(nameof(ReturnForRevisionAsync_FromPendingApproval_MovesToReturnedForRevision_AndSnapshotsContent), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = "PendingApproval";
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.ReturnForRevisionAsync(1, ReviewerUserId, "Fix the numbers in section 2");

        Assert.True(success, error);
        var reloaded = await ctx.Letters.FindAsync(1);
        Assert.Equal("ReturnedForRevision", reloaded!.Status);
        Assert.Equal(ReviewerUserId, reloaded.ReturnedByUserId);
        Assert.Equal("Fix the numbers in section 2", reloaded.ReturnReason);
        Assert.NotNull(reloaded.ReturnedDate);

        var review = Assert.Single(ctx.LetterReviews);
        Assert.Equal(LetterReviewDecisions.ReturnedForRevision, review.Decision);
        Assert.Equal("Original Subject", review.PreviousSubjectSnapshot);
        Assert.Equal("<p>Original body</p>", review.PreviousBodySnapshot);
    }

    [Fact]
    public async Task ReturnForRevisionAsync_WithoutReason_Rejected()
    {
        var service = CreateService(nameof(ReturnForRevisionAsync_WithoutReason_Rejected), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = "PendingApproval";
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.ReturnForRevisionAsync(1, ReviewerUserId, "   ");

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal("PendingApproval", (await ctx.Letters.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task RejectAsync_FromPendingApproval_MovesToRejected_AndRecordsReview()
    {
        var service = CreateService(nameof(RejectAsync_FromPendingApproval_MovesToRejected_AndRecordsReview), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = "PendingApproval";
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.RejectAsync(1, ReviewerUserId, "Out of scope entirely");

        Assert.True(success, error);
        var reloaded = await ctx.Letters.FindAsync(1);
        Assert.Equal("Rejected", reloaded!.Status);
        Assert.Equal(ReviewerUserId, reloaded.RejectedByUserId);
        Assert.Equal("Out of scope entirely", reloaded.RejectReason);

        var review = Assert.Single(ctx.LetterReviews);
        Assert.Equal(LetterReviewDecisions.Rejected, review.Decision);
    }

    [Fact]
    public async Task RejectAsync_WithoutReason_Rejected()
    {
        var service = CreateService(nameof(RejectAsync_WithoutReason_Rejected), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = "PendingApproval";
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.RejectAsync(1, ReviewerUserId, "");

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task IssueAsync_FromApproved_MovesToIssued()
    {
        var service = CreateService(nameof(IssueAsync_FromApproved_MovesToIssued), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = "Approved";
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.IssueAsync(1, ReviewerUserId);

        Assert.True(success, error);
        var reloaded = await ctx.Letters.FindAsync(1);
        Assert.Equal("Issued", reloaded!.Status);
        Assert.NotNull(reloaded.IssuedDate);
    }

    [Fact]
    public async Task IssueAsync_FromDraft_Rejected()
    {
        // یعنی گذار غیرمجاز «پرش» - نمی‌توان بدون تایید مستقیم از Draft صادر کرد
        var service = CreateService(nameof(IssueAsync_FromDraft_Rejected), out var ctx);
        ctx.Letters.Add(NewOutgoingDraft(1));
        await ctx.SaveChangesAsync();

        var (success, error) = await service.IssueAsync(1, ReviewerUserId);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task IssueAsync_BackToDraft_IsNotAPossibleTransition()
    {
        // طبق بند ۹ (نمونه‌ی سند): Issued → Draft باید ناموفق باشد - هیچ اکشنی چنین گذاری تعریف نمی‌کند،
        // پس تنها راه اثبات آن این است که هیچ اکشن معتبری این حرکت را انجام نمی‌دهد
        var service = CreateService(nameof(IssueAsync_BackToDraft_IsNotAPossibleTransition), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = "Issued";
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (submitSuccess, _) = await service.SubmitForReviewAsync(1, AuthorUserId);
        Assert.False(submitSuccess);
        Assert.Equal("Issued", (await ctx.Letters.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task CloseAsync_OutgoingFromIssued_MovesToClosed()
    {
        var service = CreateService(nameof(CloseAsync_OutgoingFromIssued_MovesToClosed), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = "Issued";
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.CloseAsync(1, ReviewerUserId);

        Assert.True(success, error);
        var reloaded = await ctx.Letters.FindAsync(1);
        Assert.Equal("Closed", reloaded!.Status);
        Assert.NotNull(reloaded.ClosedDate);
    }

    [Fact]
    public async Task CloseAsync_OutgoingFromApproved_Rejected()
    {
        // طبق بند ۳: برای Outgoing فقط از Issued - برخلاف Incoming/Internal
        var service = CreateService(nameof(CloseAsync_OutgoingFromApproved_Rejected), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = "Approved";
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.CloseAsync(1, ReviewerUserId);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task CloseAsync_IncomingFromRegistered_Succeeds()
    {
        // طبق بند ۳: نامه‌های Incoming خط‌سیر ساده‌ی خودشان را دارند - نباید مجبور به عبور از Issued شوند
        var service = CreateService(nameof(CloseAsync_IncomingFromRegistered_Succeeds), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.LetterType = "Incoming";
        letter.Status = "Registered";
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.CloseAsync(1, ReviewerUserId);

        Assert.True(success, error);
        Assert.Equal("Closed", (await ctx.Letters.FindAsync(1))!.Status);
    }

    // ==============================================================================
    // Resubmission Tests
    // ==============================================================================

    [Fact]
    public async Task ResubmitAsync_FromReturnedForRevisionByOwner_MovesToPendingApproval_ClearsCache_IncrementsRevision()
    {
        var service = CreateService(nameof(ResubmitAsync_FromReturnedForRevisionByOwner_MovesToPendingApproval_ClearsCache_IncrementsRevision), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = "ReturnedForRevision";
        letter.ReturnedByUserId = ReviewerUserId;
        letter.ReturnedDate = DateTime.Now.AddHours(-1);
        letter.ReturnReason = "Please fix the date";
        letter.RevisionCount = 0;
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.ResubmitAsync(1, "Corrected Subject", "<p>Corrected body</p>", AuthorUserId, null);

        Assert.True(success, error);
        var reloaded = await ctx.Letters.FindAsync(1);
        Assert.Equal("PendingApproval", reloaded!.Status);
        Assert.Equal(1, reloaded.RevisionCount);
        Assert.Null(reloaded.ReturnedByUserId);
        Assert.Null(reloaded.ReturnedDate);
        Assert.Null(reloaded.ReturnReason);
        Assert.Equal("Corrected Subject", reloaded.Subject);
    }

    [Fact]
    public async Task ResubmitAsync_ByNonOwner_Rejected()
    {
        var service = CreateService(nameof(ResubmitAsync_ByNonOwner_Rejected), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = "ReturnedForRevision";
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.ResubmitAsync(1, "Hijacked", "hijacked", OtherUserId, null);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal("ReturnedForRevision", (await ctx.Letters.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task ResubmitAsync_FromDraft_Rejected()
    {
        // طبق بند ۷/۳۵: Resubmit فقط از ReturnedForRevision - نه از Draft (آن SaveDraft است)
        var service = CreateService(nameof(ResubmitAsync_FromDraft_Rejected), out var ctx);
        ctx.Letters.Add(NewOutgoingDraft(1));
        await ctx.SaveChangesAsync();

        var (success, error) = await service.ResubmitAsync(1, "x", "x", AuthorUserId, null);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task MultipleReturnRounds_PreserveFullHistoryInLetterReview()
    {
        // طبق بند ۱۶ (تاریخچه‌ی چند-دوره‌ای): دو دور برگشت/بازارسال باید دو ردیف LetterReview
        // مستقل تولید کند - نه بازنویسی روی هم
        var service = CreateService(nameof(MultipleReturnRounds_PreserveFullHistoryInLetterReview), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = "PendingApproval";
        ctx.Letters.Add(letter);
        await ctx.SaveChangesAsync();

        var (r1, e1) = await service.ReturnForRevisionAsync(1, ReviewerUserId, "Round 1 issue");
        Assert.True(r1, e1);

        var (rs1, es1) = await service.ResubmitAsync(1, "Round 1 fix", "<p>fix1</p>", AuthorUserId, null);
        Assert.True(rs1, es1);

        var (r2, e2) = await service.ReturnForRevisionAsync(1, ReviewerUserId, "Round 2 issue");
        Assert.True(r2, e2);

        var (rs2, es2) = await service.ResubmitAsync(1, "Round 2 fix", "<p>fix2</p>", AuthorUserId, null);
        Assert.True(rs2, es2);

        var reviews = ctx.LetterReviews.OrderBy(x => x.CreatedDate).ToList();
        Assert.Equal(2, reviews.Count);
        Assert.All(reviews, r => Assert.Equal(LetterReviewDecisions.ReturnedForRevision, r.Decision));
        Assert.Equal("Round 1 issue", reviews[0].Comment);
        Assert.Equal("Round 2 issue", reviews[1].Comment);
        Assert.Equal(2, (await ctx.Letters.FindAsync(1))!.RevisionCount);
    }

    // ==============================================================================
    // GetDetailAsync / Reviews projection (Audit visibility)
    // ==============================================================================

    [Fact]
    public async Task GetDetailAsync_ResolvesActorNamesAndExposesLifecycleFields()
    {
        var service = CreateService(nameof(GetDetailAsync_ResolvesActorNamesAndExposesLifecycleFields), out var ctx);
        var letter = NewOutgoingDraft(1);
        letter.Status = "ReturnedForRevision";
        letter.ReturnedByUserId = ReviewerUserId;
        letter.ReturnedDate = DateTime.Now;
        letter.ReturnReason = "Needs more detail";
        letter.RevisionCount = 1;
        ctx.Letters.Add(letter);
        ctx.Users.Add(new ApplicationUser { Id = ReviewerUserId, UserName = "reviewer", FullName = "Reviewer Person" });
        ctx.LetterReviews.Add(new LetterReview
        {
            LetterReviewId = 1,
            LetterId = 1,
            ReviewerUserId = ReviewerUserId,
            Decision = LetterReviewDecisions.ReturnedForRevision,
            Comment = "Needs more detail",
            PreviousSubjectSnapshot = "Original Subject",
            PreviousBodySnapshot = "<p>Original body</p>",
            CreatedDate = DateTime.Now,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var detail = await service.GetDetailAsync(1);

        Assert.NotNull(detail);
        Assert.Equal("Reviewer Person", detail!.Letter.ReturnedByName);
        Assert.Equal("Needs more detail", detail.Letter.ReturnReason);
        Assert.Equal(1, detail.Letter.RevisionCount);
        var review = Assert.Single(detail.Reviews);
        Assert.Equal("Reviewer Person", review.ReviewerName);
        Assert.Equal(LetterReviewDecisions.ReturnedForRevision, review.Decision);
    }

    [Fact]
    public async Task GetDetailAsync_InactiveReview_ExcludedFromProjection()
    {
        // IsActive برای حذف نرم به‌کار می‌رود (بند ۱۵: بدون حذف واقعی رکورد تاریخچه)
        var service = CreateService(nameof(GetDetailAsync_InactiveReview_ExcludedFromProjection), out var ctx);
        ctx.Letters.Add(NewOutgoingDraft(1));
        ctx.LetterReviews.Add(new LetterReview
        {
            LetterReviewId = 1,
            LetterId = 1,
            Decision = LetterReviewDecisions.Approved,
            CreatedDate = DateTime.Now,
            IsActive = false
        });
        await ctx.SaveChangesAsync();

        var detail = await service.GetDetailAsync(1);

        Assert.NotNull(detail);
        Assert.Empty(detail!.Reviews);
    }
}
