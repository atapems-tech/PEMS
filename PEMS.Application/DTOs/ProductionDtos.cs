namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» فاز ۱ - DTOهای لایه‌ی Application
/// برای هسته‌ی تولید. طبق قرارداد نام‌گذاری موجود پروژه (XxxDto/XxxListDto/XxxFilter).
/// </summary>
public class ProductionOrderListDto
{
    public int ProductionOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialDescription { get; set; } = string.Empty;
    public decimal PlannedQuantity { get; set; }
    public string? UomCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public string WorkCenterCode { get; set; } = string.Empty;
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedFinishDate { get; set; }
    public int RunCount { get; set; }
}

public class ProductionOrderDto
{
    public int ProductionOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int MaterialItemId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialDescription { get; set; } = string.Empty;
    public decimal PlannedQuantity { get; set; }
    public int? UomId { get; set; }
    public string? UomCode { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedFinishDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualFinishDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ProductionWorkCenterId { get; set; }
    public string WorkCenterCode { get; set; } = string.Empty;
    public string WorkCenterName { get; set; } = string.Empty;
    public int? EquipmentId { get; set; }
    public string? EquipmentTagNumber { get; set; }
    public int? ResponsibleEmployeeId { get; set; }
    public string? ResponsibleEmployeeName { get; set; }
    public string? Notes { get; set; }
    public string? CancellationReason { get; set; }
    public string? HoldReason { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public List<ProductionRunListDto> Runs { get; set; } = new();
}

public class ProductionRunListDto
{
    public int ProductionRunId { get; set; }
    public int ProductionOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string RunNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal TotalOutputQuantity { get; set; }
    public decimal TotalLossQuantity { get; set; }
}

public class ProductionRunDto
{
    public int ProductionRunId { get; set; }
    public int ProductionOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialDescription { get; set; } = string.Empty;
    public string RunNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? WorkCenterCode { get; set; }
    public string? EquipmentTagNumber { get; set; }
    public string? OperatorEmployeeName { get; set; }
    public string? ShiftName { get; set; }
    public string? Notes { get; set; }
    public string? HoldReason { get; set; }
    public List<ProductionInputDto> Inputs { get; set; } = new();
    public List<ProductionOutputDto> Outputs { get; set; } = new();
    public List<ProductionLossDto> Losses { get; set; } = new();
    public List<ProductionEventDto> Events { get; set; } = new();
    public ProductionReconciliationDto Reconciliation { get; set; } = new();
}

public class ProductionInputDto
{
    public int ProductionInputId { get; set; }
    public int MaterialItemId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? UomCode { get; set; }
    public int? SourceProductionBatchId { get; set; }
    public string? SourceBatchNumber { get; set; }
    public bool PostedToInventory { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Notes { get; set; }
}

public class ProductionOutputDto
{
    public int ProductionOutputId { get; set; }
    public int MaterialItemId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public int ProductionBatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? UomCode { get; set; }
    public string OutputClassification { get; set; } = string.Empty;
    public bool PostedToInventory { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Notes { get; set; }
}

public class ProductionLossDto
{
    public int ProductionLossId { get; set; }
    public decimal Quantity { get; set; }
    public string? UomCode { get; set; }
    public string LossCategory { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Notes { get; set; }
}

public class ProductionEventDto
{
    public int ProductionEventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string? EmployeeName { get; set; }
    public string? Details { get; set; }
}

/// <summary>
/// طبق بند ۱۲ سند فاز: تفاوت را پنهان نمی‌کنیم - اگر Input/Output/Loss واحد اندازه‌گیری
/// مشترک نداشته باشند (چون UOM تبدیل ندارد - طبق ممیزی معماری)، این DTO آن را به‌جای
/// یک عدد گمراه‌کننده، صریحاً "IsComparable = false" گزارش می‌کند.
/// </summary>
public class ProductionReconciliationDto
{
    public bool IsComparable { get; set; }
    public string? Note { get; set; }
    public decimal TotalInputQuantity { get; set; }
    public decimal TotalOutputQuantity { get; set; }
    public decimal TotalLossQuantity { get; set; }
    public decimal? Difference { get; set; }
}

public class ProductionBatchDto
{
    public int ProductionBatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public int ProductionOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int ProductionRunId { get; set; }
    public string RunNumber { get; set; } = string.Empty;
    public int MaterialItemId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialDescription { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? UomCode { get; set; }
    public DateTime ProducedDate { get; set; }
    public string QualityStatus { get; set; } = string.Empty;
    public string? QualityStatusReason { get; set; }
    public DateTime? QualityStatusChangedDate { get; set; }
    public string? QualityStatusChangedByName { get; set; }
    public string? Notes { get; set; }
}

public class ProductionDashboardDto
{
    public int ActiveOrderCount { get; set; }
    public int ReleasedOrderCount { get; set; }
    public int ActiveRunCount { get; set; }
    public int HeldRunCount { get; set; }
    public decimal TodaysOutputQuantity { get; set; }
    public decimal TodaysLossQuantity { get; set; }
    public List<ProductionOrderListDto> ActiveOrders { get; set; } = new();
    public List<ProductionBatchDto> RecentBatches { get; set; } = new();
    public List<ProductionLossDto> RecentLosses { get; set; } = new();
    public int UnresolvedReconciliationIssueCount { get; set; }
}
