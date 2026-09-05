namespace PEMS.Application.DTOs;

/// <summary>
/// طبق ممیزی مقیاس‌پذیری (P1 - Document Register): گزینه‌های فیلتر (Project/Discipline/Status)
/// قبلاً از روی کل نتیجه‌ی GetAllAsync در حافظه Distinct می‌شدند - این‌جا سه Query کوچک و مستقل
/// (SELECT DISTINCT واقعی) جایگزین آن هستند، هر سه با همون محدودیت Scope دیسیپلین.
/// </summary>
public class DocumentRegisterFilterOptionsDto
{
    public List<string> Projects { get; set; } = new();
    public List<string> Disciplines { get; set; } = new();
    public List<string> Statuses { get; set; } = new();
}
