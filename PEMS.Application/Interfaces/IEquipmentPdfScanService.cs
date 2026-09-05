namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق درخواست: تشخیص تجهیزات از فایل PDF. این یک استخراج کامل جدول‌بندی‌شده نیست (آن یک
/// پروژه‌ی جدای هوش مصنوعی بزرگ‌تر است) - بلکه یک تشخیص «بهترین تلاش» شماره‌تگ از روی متن
/// سند (با الگوی رایج تگ‌های صنعتی) است که نتیجه‌اش را برای بازبینی و انتخاب کاربر نشان می‌دهد،
/// نه ایمپورت کور و خودکار.
/// </summary>
public interface IEquipmentPdfScanService
{
    Task<List<string>> ScanForTagCandidatesAsync(string filePath);
}
