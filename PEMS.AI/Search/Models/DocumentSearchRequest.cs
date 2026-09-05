namespace PEMS.Application.AI.Search.Models;

public class DocumentSearchRequest
{
    // دستوری که کاربر به AI می‌دهد
    public string Direction { get; set; } = string.Empty;


    // فیلترهای اختیاری

    public string? Discipline { get; set; }


    public string? EquipmentTag { get; set; }


    public string? DocumentType { get; set; }


    public string? Status { get; set; }
}