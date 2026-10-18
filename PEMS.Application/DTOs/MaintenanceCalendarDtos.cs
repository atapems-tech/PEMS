namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS — Work Request/Work Order Scheduling &amp; Maintenance Calendar»: یک ردیف تقویم =
/// یک WorkOrder واقعی با PlannedStart/PlannedFinish - هیچ Entity تقویمی جداگانه‌ای وجود ندارد؛
/// این فقط یک DTO تصویری روی همان WorkOrder است (طبق الگوی MaintenanceBacklogItemDto).
/// </summary>
public class WorkOrderCalendarItemDto
{
    public int WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string? EquipmentTagNumber { get; set; }
    public string? EquipmentDescription { get; set; }
    public string WorkOrderType { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime PlannedStart { get; set; }
    public DateTime PlannedFinish { get; set; }
    public int? AssignedTeamId { get; set; }
    public string? AssignedTeamName { get; set; }
    public int? AssignedEmployeeId { get; set; }
    public string? AssignedEmployeeName { get; set; }

    // فقط برای وضعیت‌های غیرنهایی (Open/Assigned/InProgress/OnHold) - از PlannedFinish واقعی، نه جعلی
    public bool IsOverdue { get; set; }

    // طبق بند ۱۸ درخواست: فقط تداخل کارمند (داده‌ی واقعی PlannedStart/PlannedFinish هم‌پوشان) -
    // تداخل ظرفیت تیم پیاده‌سازی نشده چون هیچ مدل Shift/ساعت‌کاری/ظرفیت در دامنه‌ی PEMS وجود ندارد.
    public bool HasEmployeeConflict { get; set; }
}

/// <summary>WorkOrder واجد شرایط برنامه‌ریزی (دیسیپلین مسئول تعیین‌شده) که هنوز PlannedStart/PlannedFinish ندارد.</summary>
public class UnscheduledWorkOrderDto
{
    public int WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string? EquipmentTagNumber { get; set; }
    public string? EquipmentDescription { get; set; }
    public string WorkOrderType { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AssignedTeamName { get; set; }
    public string? AssignedEmployeeName { get; set; }
}

public class MaintenanceCalendarResultDto
{
    public DateTime RangeStart { get; set; }
    public DateTime RangeEnd { get; set; }
    public List<WorkOrderCalendarItemDto> Items { get; set; } = new();
    public List<UnscheduledWorkOrderDto> Unscheduled { get; set; } = new();
}

public class CalendarFilterOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
