namespace PEMS.Application.DTOs;

public class StoreRequisitionListDto
{
    public int StoreRequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? WarehouseCode { get; set; }
    public string? RequestedByName { get; set; }
    public DateTime RequestedDate { get; set; }
    public string? Purpose { get; set; }
}
