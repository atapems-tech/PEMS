using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS — Enterprise Maintenance Master Data &amp; Standards Implementation» بند ۹: جدول
/// مرجع سیاست RCM («چرا این استراتژی انتخاب شد» - PM/PDM/CBM/CM/EM/RTF/RCM/...) - عمداً از
/// MaintenanceStrategy (که یک رکورد تراکنشی به‌ازای هر Equipment+FailureMode است، نه یک Master
/// Data) جدا نگه داشته شده تا نامش با آن اشتباه گرفته نشود. MaintenanceStrategy.StrategyType
/// (رشته‌ی آزاد امروز) دست‌نخورده می‌ماند - MaintenanceStrategy.MaintenanceStrategyTypeId
/// (Nullable، پایین‌تر در MaintenanceStrategy.cs) فقط یک ارتباط افزودنی/اختیاری به این جدول
/// مرجع است، نه جایگزین آن.
/// </summary>
public class MaintenanceStrategyType : BaseEntity
{
    public int MaintenanceStrategyTypeId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NameFa { get; set; }

    public string? Description { get; set; }

    public bool IsSystemDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
