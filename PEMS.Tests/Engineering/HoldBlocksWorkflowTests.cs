using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق بخش M ممیزی (بند ۱۵ - Hold Business Rules): سرور واقعاً Approve/Issue را روی مدرک Hold
/// مسدود می‌کند - این تست‌ها گارد را از طریق موتور گردش‌کار واقعی (نه شبیه‌سازی) بررسی می‌کنند.
/// </summary>
public class HoldBlocksWorkflowTests : EngineeringWorkflowTestBase
{
    private async Task<PEMS.Domain.Entities.EngineeringDocument> CreateCheckedDocumentAsync()
    {
        var doc = CreateDraftDocument();
        await WorkflowService.SubmitAsync(doc.DocumentId, null, 1, ManagerRole);
        await WorkflowService.CheckAsync(doc.DocumentId, "checked", 1, SupervisorRole);
        return doc;
    }

    [Fact]
    public async Task ApproveAsync_OnHeldDocument_Throws()
    {
        var doc = await CreateCheckedDocumentAsync();

        var holdService = new DocumentHoldService(Ctx);
        await holdService.PlaceHoldAsync(doc.DocumentId, "pending vendor input", null, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => WorkflowService.ApproveAsync(doc.DocumentId, "approve attempt", 1, ManagerRole));
    }

    [Fact]
    public async Task ApproveAsync_AfterHoldReleased_Succeeds()
    {
        var doc = await CreateCheckedDocumentAsync();

        var holdService = new DocumentHoldService(Ctx);
        await holdService.PlaceHoldAsync(doc.DocumentId, "pending vendor input", null, 1);
        await holdService.ReleaseHoldAsync(doc.DocumentId, "vendor input received", 1);

        await WorkflowService.ApproveAsync(doc.DocumentId, "approve", 1, ManagerRole);

        Assert.Equal("Approved", doc.Status);
    }

    [Fact]
    public async Task PlaceHoldAsync_OnIssuedDocument_Throws()
    {
        var doc = await CreateCheckedDocumentAsync();
        await WorkflowService.ApproveAsync(doc.DocumentId, "approve", 1, ManagerRole);
        await WorkflowService.IssueAsync(doc.DocumentId, "issue", 1, ManagerRole,
            deliveryUnitType: DocumentWorkflowService.DeliveryUnitInternalDcc);

        Assert.Equal("Issued", doc.Status);

        var holdService = new DocumentHoldService(Ctx);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => holdService.PlaceHoldAsync(doc.DocumentId, "too late", null, 1));
    }

    [Fact]
    public async Task CreateRevision_NotBlockedByHold()
    {
        // طبق تصمیم طراحی صریح‌شده: Hold کار در حال انجام روی مدرک را مسدود نمی‌کند - فقط
        // Approve/Issue (که «نهایی‌سازی» محسوب می‌شوند) را مسدود می‌کند. ساخت رویژن جدید دقیقاً
        // همان کاری است که باید برای رفع Hold انجام شود.
        var doc = CreateDraftDocument();

        var holdService = new DocumentHoldService(Ctx);
        await holdService.PlaceHoldAsync(doc.DocumentId, "needs update", null, 1);

        Ctx.DocumentRevisions.Add(new PEMS.Domain.Entities.DocumentRevision
        {
            DocumentId = doc.DocumentId,
            RevisionNo = "01",
            Status = "Draft"
        });

        // نباید هیچ استثنایی پرتاب شود
        await Ctx.SaveChangesAsync();

        var reloaded = await Ctx.EngineeringDocuments.FindAsync(doc.DocumentId);
        Assert.True(reloaded!.IsOnHold);
    }
}
