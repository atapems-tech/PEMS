using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class MaterialRequisitionItem
{
    [Key]
    public int MRItemId { get; set; }

    public int MRId { get; set; }
    public MaterialRequisition? MR { get; set; }

    public string ItemDescription { get; set; } = string.Empty;

    public string? PartNumber { get; set; }

    public double Quantity { get; set; } = 1;

    public string? Unit { get; set; }

    public string? Remarks { get; set; }
}
