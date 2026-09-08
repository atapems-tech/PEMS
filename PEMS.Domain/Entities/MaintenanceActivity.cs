using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS — Enterprise Maintenance Master Data &amp; Standards Implementation» بند ۷: کاتالوگ
/// استاندارد فعالیت‌های تعمیراتی (مثلاً "Vibration Measurement"، "Bearing Replacement") - زیر
/// یک MaintenanceGroup (مثلاً MECH-ROT) و یک MaintenanceType اصلی (مثلاً PREDICTIVE) دسته‌بندی
/// می‌شود. کاربردپذیری واقعی (کدام Equipment Class / کدام Strategy / کدام Standard) از طریق
/// جدول‌های الحاقی MaintenanceActivityEquipmentClass/StrategyType/StandardReference (پایین‌تر
/// در همین فایل) داده‌محور تعریف می‌شود - نه Hard-Code در Controller (طبق بند ۸ صریح سند).
/// جدا از JobPlan/MaintenanceTask (که قالب اجرای قابل‌استفاده‌ی مجدد برای یک WorkOrder خاص
/// هستند، نه یک تاکسونومی مرجع) - MaintenanceTask می‌تواند بعداً (فاز آینده، اختیاری) به یک
/// MaintenanceActivity ارجاع بدهد، اما این دو Entity عمداً مستقل نگه داشته شدند.
/// </summary>
public class MaintenanceActivity : BaseEntity
{
    public int MaintenanceActivityId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NameFa { get; set; }

    public string? Description { get; set; }

    public int MaintenanceGroupId { get; set; }
    public MaintenanceGroup? MaintenanceGroup { get; set; }

    // نوع اصلی/غالب این فعالیت - کاربردپذیری گسترده‌تر (چند Strategy) از طریق
    // MaintenanceActivityStrategyType پایین‌تر تعریف می‌شود، نه این‌جا.
    public int? MaintenanceTypeId { get; set; }
    public MaintenanceType? MaintenanceType { get; set; }

    public bool IsSystemDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<MaintenanceActivityEquipmentClass> ApplicableEquipmentClasses { get; set; } = new List<MaintenanceActivityEquipmentClass>();
    public ICollection<MaintenanceActivityStrategyType> ApplicableStrategyTypes { get; set; } = new List<MaintenanceActivityStrategyType>();
    public ICollection<MaintenanceActivityStandardReference> ApplicableStandardReferences { get; set; } = new List<MaintenanceActivityStandardReference>();
}

/// <summary>
/// طبق بند ۸/۱۱: کدام Equipment Class برای این فعالیت کاربردپذیر است (مثلاً Vibration
/// Measurement روی Pump/Compressor/Fan/Blower/Gearbox/Turbine) - رابطه‌ی چند-به-چند داده‌محور.
/// </summary>
public class MaintenanceActivityEquipmentClass
{
    public int MaintenanceActivityId { get; set; }
    public MaintenanceActivity? MaintenanceActivity { get; set; }

    public int EquipmentClassId { get; set; }
    public EquipmentClass? EquipmentClass { get; set; }
}

/// <summary>
/// طبق بند ۱۱: تحت کدام سیاست RCM (Strategy Type) این فعالیت معمولاً انجام می‌شود (مثلاً
/// Vibration Measurement → Predictive/CBM).
/// </summary>
public class MaintenanceActivityStrategyType
{
    public int MaintenanceActivityId { get; set; }
    public MaintenanceActivity? MaintenanceActivity { get; set; }

    public int MaintenanceStrategyTypeId { get; set; }
    public MaintenanceStrategyType? MaintenanceStrategyType { get; set; }
}

/// <summary>
/// طبق بند ۱۱: این فعالیت با کدام استاندارد صنعتی هم‌راستاست (فقط ابرداده، نه متن استاندارد).
/// </summary>
public class MaintenanceActivityStandardReference
{
    public int MaintenanceActivityId { get; set; }
    public MaintenanceActivity? MaintenanceActivity { get; set; }

    public int StandardReferenceId { get; set; }
    public StandardReference? StandardReference { get; set; }
}
