namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق سند Inventory بخش ۱۴: جابجایی وضعیت StoreRequisition (Draft→Submitted→Approved→
/// Reserved→Issued→Closed، +Rejected/Cancelled) به همراه اثرات جانبی‌اش روی
/// MaterialReservation/InventoryTransaction/MaterialStock - مثل WorkOrderLifecycleService.
/// </summary>
public interface IStoreRequisitionLifecycleService
{
    Task<(bool Success, string? Error)> AdvanceStatusAsync(
        int storeRequisitionId, string nextStatus, string? reason, int? changedByEmployeeId);
}
