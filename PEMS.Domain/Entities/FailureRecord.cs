using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class FailureRecord
{
    [Key]
    public int FailureRecordId { get; set; }

    // تجهیز خراب شده
    public int EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    // دستور کار مرتبط
    public int? WorkOrderId { get; set; }

    public WorkOrder? WorkOrder { get; set; }

    // تاریخ خرابی
    public DateTime FailureDate { get; set; }

    // حالت خرابی
    // Bearing Failure, Leakage, Electrical Fault...
    public string FailureMode { get; set; } = string.Empty;

    // علت خرابی
    public string? FailureCause { get; set; }

    // اثر خرابی
    public string? FailureEffect { get; set; }

    // ==============================================================================
    // طبق «PEMS Phase 2» بند ۱۹: ارتباط افزودنی/اختیاری به تاکسونومی مرجع جدید - رشته‌های بالا
    // (FailureMode/FailureCause) برای Backward Compatibility با رکوردهای تاریخی دست‌نخورده
    // می‌مانند؛ این دو فیلد فقط زیرساخت AI-Ready هستند.
    // ==============================================================================
    public int? FailureModeId { get; set; }
    public FailureMode? FailureModeRef { get; set; }

    public int? FailureCauseId { get; set; }
    public FailureCause? FailureCauseRef { get; set; }

    // علت ریشه‌ای
    public string? RootCause { get; set; }

    // اقدام اصلاحی
    public string? CorrectiveAction { get; set; }

    // زمان توقف تجهیز
    public decimal? DowntimeHours { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}