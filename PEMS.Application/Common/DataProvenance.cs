namespace PEMS.Application.Common;

/// <summary>
/// طبق «PEMS Phase 9 — Enterprise Reliability, RAM &amp; Predictive Maintenance Intelligence»
/// بند ۱۸: هر نتیجه‌ی تحلیلی باید منشأ خودش را صریح اعلام کند - هرگز یک عدد شبیه‌سازی‌شده/
/// پیشنهادی هوش‌مصنوعی نباید مثل یک اندازه‌گیری واقعی نمایش داده شود. این ثابت‌ها در کل لایه‌ی
/// جدید فاز ۹ (RAM/Weibull/Simulation/Predictive/AI Governance) استفاده می‌شوند؛ کد قدیمی‌تر
/// (Phase 5-8) که از رشته‌های تحت‌اللفظی مشابه (READY/NOT_READY/...) برای وضعیت داده استفاده
/// می‌کند دست‌نخورده باقی می‌ماند - این کلاس چیزی را بازنویسی نمی‌کند، فقط یک واژگان مشترک
/// جدید برای کد جدید معرفی می‌کند.
/// </summary>
public static class DataProvenance
{
    // اندازه‌گیری/ثبت واقعی مستقیم - مثلاً یک قرائت Condition Monitoring یا یک FailureRecord واقعی
    public const string Observed = "OBSERVED";

    // محاسبه‌شده مستقیم از داده‌ی OBSERVED با یک فرمول مشخص و مستندشده - مثلاً MTBF از FailureRecord
    public const string Calculated = "CALCULATED";

    // مشتق‌شده از یک نتیجه‌ی CALCULATED دیگر (نه مستقیم از داده‌ی خام) - مثلاً یک Finding که از
    // ترکیب چند معیار محاسبه‌شده به دست آمده
    public const string Derived = "DERIVED";

    // تخمین مهندسی با فرض‌های صریح، وقتی داده‌ی کافی برای یک محاسبه‌ی قطعی موجود نیست
    public const string Estimated = "ESTIMATED";

    // خروجی یک شبیه‌سازی (Monte Carlo/RAM) - هرگز نباید به‌عنوان عملکرد واقعی کارخانه معرفی شود
    public const string Simulated = "SIMULATED";

    // پیشنهاد تولیدشده توسط هوش‌مصنوعی - همیشه نیازمند بازبینی/تایید انسانی پیش از هر اقدام
    public const string AiSuggested = "AI_SUGGESTED";

    public static readonly string[] All = { Observed, Calculated, Derived, Estimated, Simulated, AiSuggested };
}

/// <summary>
/// طبق بند ۵/۳۲ سند فاز ۹: وضعیت آمادگی داده برای هر معیار تحلیلی. همان واژگانی که از فاز ۷
/// به بعد به‌صورت رشته‌ی تحت‌اللفظی در ReliabilityIntelligenceService استفاده می‌شد، این‌جا برای
/// کد جدید فاز ۹ به‌صورت ثابت متمرکز شده - بدون تغییر رفتار سرویس‌های موجود.
/// </summary>
public static class DataReadinessState
{
    public const string Ready = "READY";
    public const string Partial = "PARTIAL";
    public const string InsufficientData = "INSUFFICIENT_DATA";
    public const string NotReady = "NOT_READY";
}
