namespace PEMS.Application.DTOs;

/// <summary>
/// طبق CMMS Production Baseline v1 - CMMS-08 (Cost &amp; KPI): از روی داده‌ی واقعی موجود
/// (MaintenanceHistory، WorkOrder، MaintenancePlan، RequiredSkill) محاسبه می‌شود - بدون
/// هیچ منبع داده‌ی جدید موازی.
/// </summary>
public class CostKpiDto
{
    public decimal TotalCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal MaterialCost { get; set; }
    public decimal ContractorCost { get; set; }

    public List<CostByEquipmentItem> TopEquipmentByCost { get; set; } = new();
    public List<CostByGroupItem> CostByFunctionalLocation { get; set; } = new();
    public List<CostByGroupItem> CostByMaintenanceType { get; set; } = new();
}

public class CostByEquipmentItem
{
    public int EquipmentId { get; set; }
    public string TagNumber { get; set; } = string.Empty;
    public decimal Cost { get; set; }
}

public class CostByGroupItem
{
    public string GroupName { get; set; } = string.Empty;
    public decimal Cost { get; set; }
}

public class MaintenanceKpiDto
{
    public double? PmCompliancePercent { get; set; }
    public int PmOverdueCount { get; set; }
    public int PmActivePlanCount { get; set; }

    public int BacklogCount { get; set; }
    public decimal BacklogHours { get; set; }

    public double PlannedPercent { get; set; }
    public double UnplannedPercent { get; set; }
    public double EmergencyPercent { get; set; }

    public double? WorkOrderAgingDays { get; set; }
    public double? WorkOrderCompletionRatePercent { get; set; }

    public int TotalWorkOrdersInPeriod { get; set; }
}

public class ReliabilitySummaryDto
{
    public int EquipmentWithFailuresCount { get; set; }
    public int TotalFailureCount { get; set; }
    public double? AverageAvailabilityPercent { get; set; }
}

public class KpiDashboardDto
{
    public CostKpiDto Cost { get; set; } = new();
    public MaintenanceKpiDto Maintenance { get; set; } = new();
    public ReliabilitySummaryDto Reliability { get; set; } = new();
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
