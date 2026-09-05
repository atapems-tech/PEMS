using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS Production Baseline v1 بند ۱۰.۴: هر مرحله‌ی اجرای قابل تفکیک یک Job Plan - مثال:
/// Task 10 — Isolate equipment، Task 20 — Remove coupling guard، ... TaskNumber طبق قرارداد
/// پروژه‌های صنعتی با فاصله (10, 20, 30...) شماره‌گذاری می‌شود تا بعداً بشود مرحله‌ی میانی
/// اضافه کرد بدون شماره‌گذاری مجدد کل لیست.
/// </summary>
public class MaintenanceTask : BaseEntity
{
    public int MaintenanceTaskId { get; set; }

    public int JobPlanId { get; set; }
    public JobPlan? JobPlan { get; set; }

    public int TaskNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? EstimatedDurationMinutes { get; set; }

    public bool IsActive { get; set; } = true;
}
