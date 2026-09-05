namespace PEMS.Application.DTOs;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» فاز ۲: DTO سبک فهرست MOC - فقط ستون‌هایی که
/// Views/MOC/Index.cshtml نمایش می‌دهد.
/// </summary>
public class MOCListItemDto
{
    public int MOCId { get; set; }
    public string MOCNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public string DisciplineName { get; set; } = "";
    public string ChangeType { get; set; } = "";
    public string RiskLevel { get; set; } = "";
    public string Status { get; set; } = "";
}
