using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS Production Baseline v1 بند ۱۰.۳: دستورالعمل اجرای قابل استفاده‌ی مجدد (Reusable
/// Job Plan) - یک بار نوشته می‌شود، توسط چند MaintenancePlan/WorkOrder استفاده می‌شود.
/// جزئیات اجرا در MaintenanceTask (زیر) هستند، نه یک فیلد متنی تخت.
/// </summary>
public class JobPlan : BaseEntity
{
    public int JobPlanId { get; set; }

    public string JobPlanNumber { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    // محدوده‌ی کار - چه چیزی شامل این Job Plan می‌شود
    public string? Scope { get; set; }

    public decimal? EstimatedDurationHours { get; set; }

    public string? SafetyRequirements { get; set; }

    // ==============================================================================
    // طبق «PEMS Phase 2» بند ۲۳: JobPlan از قبل تقریباً کامل بود (RequiredMaterial/
    // RequiredSkill/Tasks/SafetyRequirements همه موجود بودند) - فقط این پیوندهای افزودنی/
    // اختیاری برای اتصال به Master Data جدید کم بودند. RequiredTools جدا اضافه نشد چون
    // JobPlanRequiredMaterial موجود همون نقش را پوشش می‌دهد (طبق بند ۲۳: «Do not create
    // duplicate Tool/Material master entities if PEMS already has them»).
    // ==============================================================================
    public int? EquipmentClassId { get; set; }
    public EquipmentClass? EquipmentClass { get; set; }

    public int? MaintenanceActivityId { get; set; }
    public MaintenanceActivity? MaintenanceActivity { get; set; }

    public int? StandardReferenceId { get; set; }
    public StandardReference? StandardReference { get; set; }

    public bool PermitRequired { get; set; }

    public bool LOTORequired { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<MaintenanceTask> Tasks { get; set; } = new List<MaintenanceTask>();
}
