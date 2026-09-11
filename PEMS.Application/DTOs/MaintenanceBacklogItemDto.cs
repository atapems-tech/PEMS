namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 8» بند ۲۲: فهرست Backlog نگهداری - همان WorkOrder های واقعی در وضعیت‌های
/// غیرنهایی (Open/Assigned/InProgress/OnHold)، صفحه‌بندی‌شده با PagedResult استاندارد (نه یک
/// ToListAsync بدون Skip/Take روی کل ناوگان). هیچ فیلد محاسباتی‌ای جعل نمی‌شود - AgeDays و
/// IsOverdue فقط از تاریخ‌های واقعی موجود (CreatedDate/DueDate) مشتق می‌شوند.
/// </summary>
public class MaintenanceBacklogItemDto
{
    public int WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string? EquipmentTagNumber { get; set; }
    public string? EquipmentDescription { get; set; }
    public string WorkOrderType { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public int AgeDays { get; set; }
    public bool IsOverdue { get; set; }
    public string? AssignedEmployeeName { get; set; }
    public string? AssignedTeamName { get; set; }
}
