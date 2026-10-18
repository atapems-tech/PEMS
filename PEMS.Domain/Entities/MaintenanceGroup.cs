using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق PEMS_CMMS_MAINTENANCE_ORGANIZATION_BLUEPRINT.md بخش ۲: Maintenance Group یک
/// گروه‌بندی تعمیراتی دقیق‌تر از Discipline است (مثلاً Rotating Equipment زیر Mechanical) -
/// مفهومی مستقل که Team باید زیرش تعریف شود، نه مستقیم زیر Discipline.
/// </summary>
public class MaintenanceGroup : BaseEntity
{
    public int MaintenanceGroupId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    // طبق «PEMS Dynamic Maintenance Organization» بخش ۵/۹: تودرتویی دلخواه (مثلاً Mechanical
    // Maintenance ⊃ Rotating Equipment) - Null یعنی گروه ریشه (زیر Discipline مستقیم). خودارجاع
    // دقیقاً هم‌الگو با Department.ParentDepartmentId؛ گروه‌های امروز (بدون این ستون) به‌طور
    // پیش‌فرض ریشه می‌مانند، هیچ داده‌ای جابه‌جا نمی‌شود.
    public int? ParentMaintenanceGroupId { get; set; }
    public MaintenanceGroup? ParentMaintenanceGroup { get; set; }
    public ICollection<MaintenanceGroup> Children { get; set; } = new List<MaintenanceGroup>();

    public string? Description { get; set; }

    // طبق «PEMS — Enterprise Maintenance Master Data & Standards Implementation» بند ۱۲:
    // مشخص می‌کند این رکورد بخشی از خط‌مبنای پیش‌فرض سیستم است (Seed شده)، نه یک گروه دلخواه
    // کاربر - فقط برای جلوگیری از Hard-Delete تصادفی خط‌مبنا استفاده می‌شود؛ رکوردهای قدیمی
    // (قبل از این فیلد) به‌طور پیش‌فرض false می‌مانند، یعنی «کاربر ساخته» تلقی می‌شوند - رفتار
    // درستی چون واقعاً همه‌شان تا امروز دستی ساخته شده بودند.
    public bool IsSystemDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
