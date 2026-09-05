namespace PEMS.Application.DTOs;

public class VendorDto
{
    public int VendorId { get; set; }
    public string Name { get; set; } = "";
    public string? VendorCode { get; set; }
    public string? Country { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string RiskLevel { get; set; } = "Unknown";
    public double? RiskScore { get; set; }
    public DateTime? RiskAssessedDate { get; set; }
    public bool IsActive { get; set; }
    public int ContractsCount { get; set; }
    public int VDRItemsCount { get; set; }
    public int OverdueVDRItemsCount { get; set; }
}
