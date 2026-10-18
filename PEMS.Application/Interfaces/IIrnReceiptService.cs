namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق ممیزی Material Management (Gap A/B) + Step 2/3: چرخه‌ی عمر کامل IRN -
/// Draft→Submitted→PendingApproval→Closed (+Cancelled، +بازگشت به Submitted روی Reject تایید).
/// طبق اصل «Received ≠ Available»: RecordDispositionAsync فقط مقدار Accepted/Rejected/Hold را
/// روی خطوط ثبت می‌کند و گردش‌کار تایید را (در Controller، از طریق IWorkflowRuntime) شروع
/// می‌کند - هیچ InventoryTransaction/MaterialStock ای تا CompleteDispositionAsync (بعد از تایید
/// واقعی QA) ساخته نمی‌شود، و فقط برای مقدار Accepted، نه Rejected/Hold.
/// </summary>
public interface IIrnReceiptService
{
    Task<(bool Success, string? Error)> SubmitAsync(int irnReceiptId, string? reason, int? changedByEmployeeId);

    Task<(bool Success, string? Error)> CancelAsync(int irnReceiptId, string? reason, int? changedByEmployeeId);

    Task<(bool Success, string? Error, int? DispositionRecordId)> RecordDispositionAsync(
        int irnReceiptId,
        IReadOnlyList<(int LineId, decimal Accepted, decimal Rejected, decimal Hold)> lineDispositions,
        string? notes, int? dispositionedByEmployeeId);

    Task<(bool Success, string? Error)> CompleteDispositionAsync(int irnReceiptId, bool approved, int? actorEmployeeId);
}
