namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS — Quick Setup &amp; Domain Import Architecture Refactoring»: هر آیتم Setup یک قاعده‌ی
/// واقعی و قطعی دارد (نه یک درصد فرضی) - یا کامل است یا نه، و اگر نیست ممکن است به دلیل یک
/// پیش‌نیاز غایب Blocked باشد. درصد نمایشی کلی صفحه از نسبت واقعی
/// (تعداد آیتم‌های کامل / کل آیتم‌ها) محاسبه می‌شود، نه یک عدد ثابت.
/// </summary>
public enum SetupStepStatus
{
    Complete,
    Blocked,
    NotStarted
}

public class SetupStepDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SetupStepStatus Status { get; set; }
    public List<string> MissingDependencies { get; set; } = new();
    public string? ActionController { get; set; }
    public string? ActionAction { get; set; }
    public string? ActionLabel { get; set; }
}

public class SetupStatusDto
{
    public List<SetupStepDto> Steps { get; set; } = new();
    public int CompletedCount => Steps.Count(x => x.Status == SetupStepStatus.Complete);
    public int TotalCount => Steps.Count;
    public int PercentComplete => TotalCount == 0 ? 0 : (int)Math.Round(CompletedCount * 100.0 / TotalCount);
    public bool IsFullyComplete => TotalCount > 0 && CompletedCount == TotalCount;
}
