using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق بازنگری CMMS و استاندارد ISO 14224 (Reliability data collection for O&amp;G/petrochemical
/// equipment): برای تحلیل قابلیت‌اطمینان واقعی (MTBF، الگوی خرابی تکراری)، هر دستور کار
/// اصلاحی/اضطراری باید حالت خرابی، علت خرابی، و زمان واقعی از دست‌رفته (Downtime) را ثبت کند -
/// نه فقط توضیح متنی آزاد.
/// </summary>
public class WorkOrder : BaseEntity
{
    [Key]
    public int WorkOrderId { get; set; }

    // تجهیز مرتبط
    public int EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    // برنامه نگهداری مرتبط (اختیاری)
    public int? MaintenancePlanId { get; set; }

    public MaintenancePlan? MaintenancePlan { get; set; }

    // طبق CMMS Production Baseline v1 بند P0-05/۹.۱: WorkRequest منشأ این WorkOrder (در صورت
    // وجود) - مسیر مجاز ساخت کار اجرایی. WorkOrder هایی که مستقیم ساخته شده‌اند (بدون
    // WorkRequest) این فیلد را Null نگه می‌دارند - کنترل این مسیر مستقیم بخشی از فاز بعدی است.
    public int? SourceWorkRequestId { get; set; }
    public WorkRequest? SourceWorkRequest { get; set; }

    // شماره دستور کار
    // مثال: WO-2026-0001
    public string WorkOrderNumber { get; set; } = string.Empty;

    // نوع کار
    // Preventive, Corrective, Emergency
    public string WorkOrderType { get; set; } = string.Empty;

    // اولویت
    // Low, Medium, High, Critical
    public string Priority { get; set; } = "Medium";

    // وضعیت
    // Open, Assigned, InProgress, Completed, Closed
    public string Status { get; set; } = "Open";

    // مسئول انجام کار - طبق درخواست، حالا به گروه کاری/کارمند واقعی وصل می‌شه، نه فقط متن آزاد.
    // فیلد قدیمی AssignedTo برای سازگاری با داده‌ی قبلی نگه داشته شده (یادداشت آزاد اضافی).
    public string? AssignedTo { get; set; }

    public int? AssignedTeamId { get; set; }
    public Team? AssignedTeam { get; set; }

    public ICollection<WorkOrderLabor> LaborEntries { get; set; } = new List<WorkOrderLabor>();

    public int? AssignedEmployeeId { get; set; }
    public Employee? AssignedEmployee { get; set; }

    // ==============================================================================
    // طبق ISO 14224: برای WorkOrderType های Corrective/Emergency، این فیلدها لازم‌اند تا
    // بشه تحلیل قابلیت‌اطمینان (الگوی خرابی تکراری، MTBF) روی داده‌ی واقعی انجام داد - نه
    // فقط یک توضیح متنی آزاد که برای تحلیل آماری قابل‌گروه‌بندی نیست.
    // ==============================================================================

    // Leakage, Fracture, Vibration, Overheating, Corrosion, Blockage, ElectricalFault, ...
    public string? FailureModeCode { get; set; }

    // Corrosion, Fatigue, Wear, OperatorError, DesignDeficiency, MaterialDefect, ...
    public string? FailureCauseCode { get; set; }

    // ==============================================================================
    // طبق «PEMS Phase 2» بند ۱۹: ارتباط افزودنی/اختیاری به تاکسونومی مرجع جدید - رشته‌های بالا
    // (FailureModeCode/FailureCauseCode) برای Backward Compatibility با رکوردهای تاریخی و کد
    // موجود دست‌نخورده می‌مانند؛ این دو فیلد فقط زیرساخت AI-Ready هستند.
    // ==============================================================================
    public int? FailureModeId { get; set; }
    public FailureMode? FailureModeRef { get; set; }

    public int? FailureCauseId { get; set; }
    public FailureCause? FailureCauseRef { get; set; }

    public string? RootCause { get; set; }

    // ساعت واقعی توقف تولید/عملکرد ناشی از این خرابی - برای محاسبه‌ی OEE/Availability، جدا
    // از StartDate/CompletionDate خودِ کار تعمیر (که ممکنه دیرتر از توقف واقعی شروع بشه)
    public decimal? DowntimeHours { get; set; }

    // ==============================================================================
    // طبق CMMS Production Baseline v1 - CMMS-08 (Cost & KPI): برخلاف Labor/Material که از
    // WorkOrderLabor/MaintenanceSpareUsage محاسبه می‌شوند، هزینه‌ی پیمانکار منبع داده‌ی جداگانه
    // ندارد - این‌جا در لحظه‌ی تکمیل (برای هر نوع WorkOrder، نه فقط Corrective/Emergency) ثبت
    // می‌شود و در MaintenanceHistory.Cost جمع می‌شود.
    // ==============================================================================
    public decimal? ContractorCost { get; set; }
    public string? ContractorName { get; set; }

    // تاریخ شروع
    public DateTime? StartDate { get; set; }

    // تاریخ پایان
    public DateTime? CompletionDate { get; set; }

    // شرح فعالیت
    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}