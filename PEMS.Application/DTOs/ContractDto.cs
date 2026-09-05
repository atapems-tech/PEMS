namespace PEMS.Application.DTOs;

public class ContractDto
{
    public int ContractId { get; set; }
    public string ContractNo { get; set; } = "";
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public int VendorId { get; set; }
    public string VendorName { get; set; } = "";
    public string? ScopeOfWork { get; set; }
    public decimal? ContractValue { get; set; }
    public string? Currency { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "";
    public int PurchaseOrdersCount { get; set; }
}
