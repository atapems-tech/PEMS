namespace PEMS.Application.DTOs;

public class AssetListDto
{
    public int AssetId { get; set; }
    public string AssetNumber { get; set; } = "";
    public int EquipmentId { get; set; }
    public string EquipmentTagNumber { get; set; } = "";
    public string EquipmentName { get; set; } = "";
    public string? MaintenanceClass { get; set; }
    public string Status { get; set; } = "";
    public DateTime? CommissioningDate { get; set; }
    public string? CriticalityClass { get; set; }
    public int OpenWorkOrderCount { get; set; }
    public int? FunctionalLocationId { get; set; }
    public string? FunctionalLocationBreadcrumb { get; set; }
}

/// <summary>
/// جزئیات کامل Asset - چرخه‌عمر CMMS یک Equipment: نگهداری، بازرسی، خرابی و هزینه، طبق
/// PEMS_CMMS_ARCHITECTURE.md بخش ۴.
/// </summary>
public class AssetDetailDto
{
    public int AssetId { get; set; }
    public string AssetNumber { get; set; } = "";
    public int EquipmentId { get; set; }
    public string EquipmentTagNumber { get; set; } = "";
    public string EquipmentName { get; set; } = "";
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? FunctionalLocation { get; set; }
    public int? FunctionalLocationId { get; set; }
    public string? FunctionalLocationBreadcrumb { get; set; }
    public string? MaintenanceClass { get; set; }
    public DateTime? CommissioningDate { get; set; }
    public string Status { get; set; } = "";
    public string? Description { get; set; }
    public string? CriticalityClass { get; set; }
    public string? RiskLevel { get; set; }
    public bool IsActive { get; set; }

    public List<WorkOrderSummaryDto> WorkOrders { get; set; } = new();
    public List<InspectionSummaryDto> Inspections { get; set; } = new();
    public List<FailureSummaryDto> Failures { get; set; } = new();
    public MaintenanceHistorySummaryDto? LatestMaintenance { get; set; }
    public decimal TotalCost { get; set; }
}

/// <summary>
/// خلاصه‌ی Asset برای نمایش داخل تب Equipment/Details - جزئیات کامل همچنان روی صفحه‌ی
/// اختصاصی Asset/Details می‌ماند، دقیقاً مثل الگوی InstrumentPassportSummaryDto.
/// </summary>
public class AssetSummaryDto
{
    public int AssetId { get; set; }
    public string AssetNumber { get; set; } = "";
    public string? FunctionalLocation { get; set; }
    public string? FunctionalLocationBreadcrumb { get; set; }
    public string? MaintenanceClass { get; set; }
    public DateTime? CommissioningDate { get; set; }
    public string Status { get; set; } = "";
    public string? CriticalityClass { get; set; }
}

public class InspectionSummaryDto
{
    public int InspectionRecordId { get; set; }
    public string? InspectionType { get; set; }
    public DateTime? InspectionDate { get; set; }
    public string? Inspector { get; set; }
    public string? Result { get; set; }
}

public class FailureSummaryDto
{
    public int FailureRecordId { get; set; }
    public DateTime FailureDate { get; set; }
    public string FailureMode { get; set; } = "";
    public string? FailureCause { get; set; }
    public decimal? DowntimeHours { get; set; }
    public int? WorkOrderId { get; set; }
}

public class MaintenanceHistorySummaryDto
{
    public DateTime ExecutionDate { get; set; }
    public string? ActionTaken { get; set; }
    public string? Technician { get; set; }
    public decimal? DurationHours { get; set; }
    public decimal? Cost { get; set; }
}
