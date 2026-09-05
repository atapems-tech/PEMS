namespace PEMS.Application.DTOs;

public class PurchaseOrderDto
{
    public int PoId { get; set; }
    public string PoNumber { get; set; } = "";
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public int VendorId { get; set; }
    public string VendorName { get; set; } = "";
    public int? ContractId { get; set; }
    public string? ContractNo { get; set; }
    public string? Description { get; set; }
    public decimal? Value { get; set; }
    public string Status { get; set; } = "";
    public int VDRItemsCount { get; set; }
}
