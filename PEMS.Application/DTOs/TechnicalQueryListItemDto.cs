namespace PEMS.Application.DTOs;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» فاز ۲: DTO سبک فهرست TQ/TSR/RFI - فقط
/// ستون‌هایی که Views/TechnicalQuery/Index.cshtml نمایش می‌دهد.
/// </summary>
public class TechnicalQueryListItemDto
{
    public int TechnicalQueryId { get; set; }
    public string QueryNumber { get; set; } = "";
    public string QueryType { get; set; } = "";
    public string Subject { get; set; } = "";
    public string DisciplineName { get; set; } = "";
    public string Priority { get; set; } = "";
    public DateTime? RequiredResponseDate { get; set; }
    public string Status { get; set; } = "";
    public bool IsOverdue { get; set; }
}
