namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۴: نتیجه‌ی یک شبیه‌سازی Monte Carlo روی توزیع Weibull واقعاً
/// برازش‌شده (نه یک توزیع فرضی). Provenance همیشه SIMULATED است و هرگز نباید به‌عنوان عملکرد
/// واقعی کارخانه نمایش داده شود - این ماهیتاً یک برون‌یابی احتمالاتی از پارامترهای محاسبه‌شده
/// است، نه یک اندازه‌گیری.
/// </summary>
public class ReliabilitySimulationDto
{
    public int EquipmentId { get; set; }

    // READY, NOT_READY - فقط وقتی Weibull و MTTR واقعی هر دو آماده باشند اجرا می‌شود
    public string DataStatus { get; set; } = "NOT_READY";
    public string? Reason { get; set; }

    public int Iterations { get; set; }
    public double SimulatedHorizonHours { get; set; }

    public double? MeanAvailabilityPercent { get; set; }
    public double? P10AvailabilityPercent { get; set; }
    public double? P90AvailabilityPercent { get; set; }
    public double? MeanDowntimeHours { get; set; }
    public double? MeanFailureCount { get; set; }

    public double? InputBeta { get; set; }
    public double? InputEta { get; set; }
    public double? InputMTTRHours { get; set; }

    public string Provenance { get; set; } = "SIMULATED";
    public string MethodologyNote { get; set; } =
        "زمان بین خرابی از Weibull(β,η) برازش‌شده و زمان تعمیر از توزیع نمایی با میانگین MTTR واقعی نمونه‌برداری می‌شود - یک ساده‌سازی مستندشده، نه ادعای یک توزیع تعمیر واقعی اندازه‌گیری‌شده.";
}
