using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

public class Equipment : BaseEntity
{
    [Key]
    public int EquipmentId { get; set; }

    // شماره تگ تجهیز
    public string TagNumber { get; set; } = string.Empty;

    // نام تجهیز
    public string Name { get; set; } = string.Empty;

    // نوع تجهیز
    // Pump, Motor, Valve, Instrument, Tank ...
    public string EquipmentType { get; set; } = string.Empty;

    // ارتباط با پروژه
    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    // ارتباط با دیسیپلین
    public int DisciplineId { get; set; }

    public Discipline? Discipline { get; set; }

    // سازنده
    public string? Manufacturer { get; set; }

    // مدل
    public string? Model { get; set; }

    // شماره سریال
    public string? SerialNumber { get; set; }

    // وضعیت تجهیز
    // Active, Spare, Retired
    public string Status { get; set; } = "Active";

    public string? Description { get; set; }

    // طبق درخواست: ایمپورت اکسل تجهیزات (CMMS) - این فیلدها در فایل‌های واقعی وجود دارند
    public string? Area { get; set; }
    public string? PIDNumber { get; set; }

    // ==============================================================================
    // طبق CMMS Production Baseline v1 بند ۷: رابطه‌ی واقعی به FunctionalLocation، جدا از
    // فیلد متنی Area بالا. Area خام (از ایمپورت) دست‌نخورده می‌ماند - اتصال واقعی فقط با
    // تایید صریح انسان انجام می‌شود، دقیقاً مثل الگوی MRNumberRaw/PONumberRaw پایین‌تر.
    // ==============================================================================
    public int? FunctionalLocationId { get; set; }
    public FunctionalLocation? FunctionalLocationRef { get; set; }

    // ساختار درختی تجهیزات (Parent/Child) - طبق بند ۷ سند Baseline
    public int? ParentEquipmentId { get; set; }
    public Equipment? ParentEquipment { get; set; }
    public ICollection<Equipment> ChildEquipments { get; set; } = new List<Equipment>();

    // تاریخ نصب - جدا از Asset.CommissioningDate (که تاریخ راه‌اندازی چرخه‌عمر CMMS است)
    public DateTime? InstallationDate { get; set; }

    public int? RelatedMRId { get; set; }
    public MaterialRequisition? RelatedMR { get; set; }

    // ==============================================================================
    // طبق رفع باگ ایمپورت تجهیزات: قبلاً ایمپورت از روی همین شماره‌ها، خودکار یک
    // MaterialRequisition/Vendor/VDRItem جدید می‌ساخت (حدسی، بدون بازبینی). حالا ایمپورت
    // فقط این دو فیلد را به‌عنوان متن خام ذخیره می‌کند؛ اتصال واقعی (RelatedMRId یا
    // VDRItem.EquipmentId) فقط بعد از تایید کاربر در Relationship Explorer انجام می‌شود.
    // ==============================================================================
    public string? MRNumberRaw { get; set; }
    public string? PONumberRaw { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // طبق Phase E-04 (رفع P1 ممیزی Reverse Traceability): مدارک مهندسی مرتبط با این تجهیز
    // (P&ID، دیتاشیت، منوال و ...) - سمت معکوس EngineeringDocument.EquipmentId
    public ICollection<EngineeringDocument> EngineeringDocuments { get; set; } = new List<EngineeringDocument>();
}