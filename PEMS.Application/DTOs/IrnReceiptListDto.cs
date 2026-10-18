namespace PEMS.Application.DTOs;

public class IrnReceiptListDto
{
    public int IrnReceiptId { get; set; }
    public string IrnNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? VendorName { get; set; }
    public string? WarehouseCode { get; set; }
    public string? ReceivedByName { get; set; }
    public DateTime ReceivingDate { get; set; }
    public int LineCount { get; set; }
}
