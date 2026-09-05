namespace PEMS.Domain.Entities;

/// <summary>
/// نوع تخصصی "مدرک قرارداد" از Enterprise Document Core. نسخه‌ی اولیه/حداقلی - توسعه‌ی کامل
/// (Claims, Correspondence و ...) در فاز بعدی Contract Management انجام می‌شود.
/// </summary>
public class ContractDocument : Document
{
    public int ContractId { get; set; }
    public Contract? Contract { get; set; }

    public string? ClauseReference { get; set; }
}
