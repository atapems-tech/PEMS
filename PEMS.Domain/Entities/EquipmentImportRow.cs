namespace PEMS.Domain.Entities;

public class EquipmentImportRow
{
    public int EquipmentImportRowId { get; set; }

    public int EquipmentImportBatchId { get; set; }
    public EquipmentImportBatch? Batch { get; set; }

    public int RowNumber { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    public bool IncludeInCommit { get; set; } = true;

    // Valid, Warning, Error
    public string ValidationStatus { get; set; } = "Valid";
    public string? ValidationMessage { get; set; }

    public bool IsUpdate { get; set; }

    public int? CreatedEquipmentId { get; set; }
}
