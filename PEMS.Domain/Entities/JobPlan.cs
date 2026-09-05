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

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<MaintenanceTask> Tasks { get; set; } = new List<MaintenanceTask>();
}
