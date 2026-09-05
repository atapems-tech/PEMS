namespace PEMS.Application.DTOs;

public class MaterialItemListDto
{
    public int MaterialItemId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? DisciplineName { get; set; }
    public string? UomCode { get; set; }
    public string? Manufacturer { get; set; }
    public string? VendorName { get; set; }
    public bool IsStockItem { get; set; }
    public bool IsCritical { get; set; }
    public bool IsActive { get; set; }
    public decimal TotalOnHand { get; set; }
    public decimal TotalAvailable { get; set; }
    public bool IsLowStock { get; set; }
}

public class MaterialItemDetailDto
{
    public int MaterialItemId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string? MaterialType { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? LongDescription { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int? DisciplineId { get; set; }
    public string? DisciplineName { get; set; }
    public int? UomId { get; set; }
    public string? UomCode { get; set; }
    public string? Manufacturer { get; set; }
    public string? ManufacturerPartNumber { get; set; }
    public int? VendorId { get; set; }
    public string? VendorName { get; set; }
    public string? Specification { get; set; }
    public string? Model { get; set; }
    public string? Size { get; set; }
    public string? MaterialGrade { get; set; }
    public bool IsSerialized { get; set; }
    public bool IsBatchControlled { get; set; }
    public bool IsStockItem { get; set; }
    public bool IsCritical { get; set; }
    public bool IsActive { get; set; }
    public List<MaterialStockRowDto> Stocks { get; set; } = new();
    public List<InventoryTransactionRowDto> RecentTransactions { get; set; } = new();

    // طبق سند Metadata Extraction بخش ۱۰ (UI Requirements): ردیابی تا Import Batch مبدأ -
    // یک متریال ممکن است از چند Batch/فایل مختلف (وارداتِ مکرر) تغذیه شده باشد
    public List<MaterialSourceImportDto> SourceImports { get; set; } = new();
}

public class MaterialSourceImportDto
{
    public int MrsImportBatchId { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public DateTime ImportDate { get; set; }
    public string? RawMrsNo { get; set; }
    public string? RawSupplier { get; set; }
    public string? RawPoNo { get; set; }
}

public class MaterialStockRowDto
{
    public int MaterialStockId { get; set; }
    public int WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string? StorageLocationCode { get; set; }
    public decimal OnHandQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal? MinimumQuantity { get; set; }
    public decimal? MaximumQuantity { get; set; }
    public decimal? ReorderPoint { get; set; }
    public bool IsLowStock { get; set; }
    public bool IsOutOfStock { get; set; }
}

public class InventoryTransactionRowDto
{
    public int InventoryTransactionId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialDescription { get; set; } = string.Empty;
    public string WarehouseCode { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Reason { get; set; }
}
