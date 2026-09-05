namespace PEMS.Application.DTOs;

public class EquipmentDto
{
    public int EquipmentId { get; set; }
    public string TagNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public string EquipmentType { get; set; } = "";
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public int DisciplineId { get; set; }
    public string DisciplineName { get; set; } = "";
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string Status { get; set; } = "";
    public string? Description { get; set; }
    public string? Area { get; set; }
    public string? PIDNumber { get; set; }

    // طبق CMMS Production Baseline v1 بند ۷ (CMMS-01: Asset Foundation)
    public int? FunctionalLocationId { get; set; }
    public string? FunctionalLocationBreadcrumb { get; set; }
    public int? ParentEquipmentId { get; set; }
    public string? ParentEquipmentTagNumber { get; set; }
    public DateTime? InstallationDate { get; set; }
    public int ChildEquipmentCount { get; set; }
    public List<ChildEquipmentSummaryDto> ChildEquipments { get; set; } = new();

    // طبق بازنگری CMMS: کلاس بحرانی‌بودن (A/B/C) از تحلیل EquipmentCriticality، برای نمایش
    // سریع روی پاسپورت تجهیز
    public string? CriticalityClass { get; set; }
    public int DocumentsCount { get; set; }
    public int VDRItemsCount { get; set; }
    public int WorkOrdersCount { get; set; }
}

public class ChildEquipmentSummaryDto
{
    public int EquipmentId { get; set; }
    public string TagNumber { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>
/// Digital Equipment Passport: نمای یکپارچه‌ی یک تجهیز طبق سند راهنما —
/// Equipment Info + Engineering Documents + Vendor Data (VDR) + MR/MRQ + Maintenance History.
/// </summary>
public class EquipmentPassportDto
{
    public EquipmentDto Equipment { get; set; } = new();

    public List<EngineeringDocumentDto> Documents { get; set; } = new();

    public List<VDRItemDto> VDRItems { get; set; } = new();

    public List<MRDto> RelatedMRs { get; set; } = new();

    public List<WorkOrderSummaryDto> WorkOrders { get; set; } = new();

    // طبق درخواست: کلیک روی شماره P&ID باید مستقیم به مدرک مهندسی متناظر منتقل کند
    public EngineeringDocumentDto? PIDDocument { get; set; }

    // طبق فاز ۳ نقشه‌راه معماری: تعمیق محوریت Asset - تاریخچه‌ی کامل تجهیز شامل NCR، MOC، TQ/TSR/RFI
    public List<RelationshipNodeDto> NonConformanceReports { get; set; } = new();
    public List<RelationshipNodeDto> MOCs { get; set; } = new();
    public List<RelationshipNodeDto> TechnicalQueries { get; set; } = new();

    // رابطه‌های پیشنهادی‌ای که Relationship Explorer پیدا کرده ولی هنوز کاربر تاییدشان
    // نکرده - طبق درخواست: "تمام اطلاعات ثبت‌شده برای تشخیص رابطه استفاده شود"
    public List<RelationshipNodeDto> SuggestedLinks { get; set; } = new();

    // طبق درخواست: شناسنامه‌ی ابزاردقیق (در صورت وجود) باید بخشی از همین صفحه باشد، نه فقط
    // در صفحه‌ی جدای Instrument/Passport. null یعنی این تجهیز پروفایل ابزاردقیق ندارد.
    public InstrumentPassportSummaryDto? Instrument { get; set; }

    // طبق PEMS_CMMS_ARCHITECTURE.md: چرخه‌عمر CMMS این تجهیز (در صورت وجود Asset)، دقیقاً
    // مثل الگوی Instrument بالا - جزئیات کامل روی صفحه‌ی اختصاصی Asset/Details.
    public AssetSummaryDto? Asset { get; set; }
}

/// <summary>
/// خلاصه‌ی پاسپورت ابزاردقیق برای نمایش داخل تب Equipment/Details - جزئیات کامل + ثبت کالیبراسیون
/// + چاپ همچنان روی صفحه‌ی اختصاصی Instrument/Passport می‌مانند تا آن صفحه‌ی تست‌شده دست‌نخورده بماند.
/// </summary>
public class InstrumentPassportSummaryDto
{
    public int InstrumentId { get; set; }
    public string InstrumentType { get; set; } = "";
    public string? LoopNumber { get; set; }
    public string? System { get; set; }
    public string? SignalClass { get; set; }
    public string? ProcessVariable { get; set; }
    public decimal? RangeMin { get; set; }
    public decimal? RangeMax { get; set; }
    public string? Unit { get; set; }
    public string? OutputSignal { get; set; }
    public string? HazardousArea { get; set; }
    public string? ExProtection { get; set; }
    public string? SIL { get; set; }
    public string? JunctionBox { get; set; }
    public string? MarshallingCabinet { get; set; }
    public string? CableNumber { get; set; }
    public string? IOCardChannel { get; set; }
    public string? SystemCabinet { get; set; }
    public int? CalibrationInterval { get; set; }
    public DateTime? LastCalibrationDate { get; set; }
    public DateTime? NextCalibrationDue { get; set; }
    public string CalibrationStatus { get; set; } = "Unknown";
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime? NextMaintenanceDue { get; set; }
    public string CurrentStatus { get; set; } = "";
}

public class WorkOrderSummaryDto
{
    public int WorkOrderId { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "";
    public DateTime? CreatedDate { get; set; }
}
