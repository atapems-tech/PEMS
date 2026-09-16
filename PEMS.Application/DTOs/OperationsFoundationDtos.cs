namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Operations Phase 2A — Foundational Hardening»: DTOهای Target/Downtime/موجودی
/// بچ-آگاه/گزارش پایه‌ی Operations. طبق قرارداد نام‌گذاری موجود پروژه.
/// </summary>
public class ProductionTargetDto
{
    public int ProductionTargetId { get; set; }
    public DateTime TargetDate { get; set; }
    public int? ProductionOrderId { get; set; }
    public string? OrderNumber { get; set; }
    public int MaterialItemId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public int ProductionWorkCenterId { get; set; }
    public string WorkCenterCode { get; set; } = string.Empty;
    public int? ShiftDefinitionId { get; set; }
    public string? ShiftCode { get; set; }
    public decimal TargetQuantity { get; set; }
    public string? UomCode { get; set; }
    public bool IsActive { get; set; }
}

public class ProductionDowntimeEventDto
{
    public int ProductionDowntimeEventId { get; set; }
    public int ProductionRunId { get; set; }
    public string RunNumber { get; set; } = string.Empty;
    public int ProductionWorkCenterId { get; set; }
    public string WorkCenterCode { get; set; } = string.Empty;
    public int? EquipmentId { get; set; }
    public string? EquipmentTagNumber { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    // همیشه از StartTime/EndTime مشتق می‌شود - هرگز مستقیم ذخیره نشده (طبق کامنت Entity)
    public decimal? DurationMinutes => EndTime.HasValue ? (decimal)(EndTime.Value - StartTime).TotalMinutes : null;

    public string CauseCategory { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? SourceWorkRequestId { get; set; }
    public int? SourceWorkOrderId { get; set; }
}

/// <summary>
/// طبق بخش ۱۲ سند فاز: مدل Read (نه یک لجر دوم) - همیشه از InventoryTransaction محاسبه می‌شود.
/// QualityStatus از ProductionBatch.QualityStatus می‌آید (تکرار/کپی نشده - فقط Join).
/// </summary>
public class BatchInventoryBalanceRowDto
{
    public int MaterialItemId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public int? StorageLocationId { get; set; }
    public string? StorageLocationCode { get; set; }
    public int ProductionBatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string QualityStatus { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string? UomCode { get; set; }
}

/// <summary>
/// طبق بخش ۲۱-۲۲ سند فاز: فقط معیارهایی که تعریف‌شان امروز قطعی است - Gross/Net عمداً این‌جا
/// نیست (بند ۴ سند فاز: تصمیم کسب‌وکاری حل‌نشده، حدس زده نشده).
/// </summary>
public class OperationsReportSummaryDto
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal TotalOutputQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public decimal PendingQcQuantity { get; set; }
    public decimal BlockedQuantity { get; set; }
    public decimal TotalLossQuantity { get; set; }
    public decimal TotalTargetQuantity { get; set; }

    // Null یعنی هدفی برای این بازه ثبت نشده - صفر جایگزین نمی‌شود (گمراه‌کننده می‌بود)
    public decimal? VarianceQuantity => TotalTargetQuantity == 0 ? null : TotalOutputQuantity - TotalTargetQuantity;
}
