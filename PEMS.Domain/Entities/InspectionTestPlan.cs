using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// Inspection &amp; Test Plan - سند مرکزی QC در هر پروژه‌ی EPC طبق API Q1/Q2 و ISO 17020.
/// قبل از این، هیچ مفهومی از ITP در سیستم وجود نداشت - InspectionRecord مستقیم به تجهیز
/// وصل می‌شد بدون هیچ برنامه‌ی از‌پیش‌تعریف‌شده‌ای (که یعنی امکان پیگیری Hold/Witness Point
/// طبق استاندارد وجود نداشت).
/// </summary>
public class InspectionTestPlan : BaseEntity
{
    public int InspectionTestPlanId { get; set; }

    public string ITPNumber { get; set; } = string.Empty;

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    // تجهیز یا بسته‌ای که این ITP برایش تعریف شده (اختیاری - ITP می‌تواند برای یک بسته/فعالیت
    // کلی هم باشد، نه لزوماً یک تجهیز خاص)
    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    // طبق ممیزی Package Procurement Contract Finalization (§C/§E، Phase 5.1): پیش از این ITP
    // فقط در سطح تجهیز قابل تعریف بود - این دو FK اختیاری (هم‌الگوی EquipmentId بالا) امکان
    // بازرسی الزامی در سطح بسته/قرارداد را اضافه می‌کنند، بدون شکستن رفتار موجود سطح-تجهیز.
    public int? ProcurementPackageId { get; set; }
    public ProcurementPackage? ProcurementPackage { get; set; }

    public int? ContractId { get; set; }
    public Contract? Contract { get; set; }

    public int QCDisciplineId { get; set; }
    public QCDiscipline? QCDiscipline { get; set; }

    public string Title { get; set; } = string.Empty;

    // استاندارد/کد مرجع - مثلاً ASME Section IX, API 650, ASME V
    public string? ReferenceCodeStandard { get; set; }

    public int Revision { get; set; } = 0;

    // Draft, Approved, Active, Closed, Superseded
    public string Status { get; set; } = "Draft";

    public DateTime? ApprovedDate { get; set; }
    public int? ApprovedById { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<ITPLineItem> LineItems { get; set; } = new List<ITPLineItem>();
}
