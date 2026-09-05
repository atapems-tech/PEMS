using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// ==============================================================================
/// طبق سند معماری Labor Cost: AssignedEmployee فقط نشان می‌دهد چه کسی مسئول اجرای
/// WorkOrder است؛ این Entity ثبت واقعی می‌کند که کدام کارمندها، چند ساعت، با چه نرخی واقعاً
/// روی این WorkOrder کار کرده‌اند. Rate/OvertimeRate/LaborCost این‌جا Snapshot می‌شوند - اگر
/// نرخ Employee بعداً تغییر کند، هزینه‌ی این رکورد تاریخی تغییر نمی‌کند.
/// ==============================================================================
/// </summary>
public class WorkOrderLabor : BaseEntity
{
    public int WorkOrderLaborId { get; set; }

    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    public DateTime WorkDate { get; set; } = DateTime.Now;

    public decimal RegularHours { get; set; }
    public decimal OvertimeHours { get; set; }

    // Snapshot - در لحظه‌ی ثبت از EmployeeLaborRate خوانده و این‌جا ذخیره می‌شود، هرگز
    // بعداً بازخوانی/به‌روزرسانی نمی‌شود
    public decimal Rate { get; set; }
    public decimal OvertimeRate { get; set; }

    public decimal LaborCost { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
