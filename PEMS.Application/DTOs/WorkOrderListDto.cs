namespace PEMS.Application.DTOs;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): ردیف Grid صفحه‌ی
/// WorkOrder/Index - قبلاً کل Entity Graph (WorkOrder+Equipment+AssignedTeam+AssignedEmployee)
/// با سه Include و بدون Skip/Take برگردانده می‌شد. این DTO فقط همون ستون‌هایی است که View
/// واقعاً نمایش می‌دهد.
/// </summary>
public class WorkOrderListDto
{
    public int WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = "";
    public int EquipmentId { get; set; }
    public string? EquipmentTagNumber { get; set; }
    public string WorkOrderType { get; set; } = "";
    public string Priority { get; set; } = "";
    public string? AssignedTeamName { get; set; }
    public string? AssignedEmployeeName { get; set; }
    public string? AssignedTo { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedDate { get; set; }
}

/// <summary>
/// شمارنده‌های Dashboard صفحه‌ی WorkOrder/Index - عمداً روی کل WorkOrderهای فعال محاسبه
/// می‌شوند (مستقل از فیلتر status/equipmentId/assignedToMe فعلی Grid)، دقیقاً همون رفتار
/// قبلی - طبق بند G-5 دستورالعمل فاز ۶B: "اگر Counter عمداً Global است در حالی که Grid
/// فیلترشده، آن رفتار را حفظ کن".
/// </summary>
public class WorkOrderDashboardDto
{
    public int OpenCount { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }
    public int CriticalOpenCount { get; set; }
}
