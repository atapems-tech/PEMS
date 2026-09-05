using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// آستانه‌های Confidence Gate (طبق سند: High=خودکار، Medium=بازبینی مهندسی، Low=دسته‌بندی دستی).
/// یک رکورد Singleton که از Admin قابل تنظیم است، بدون نیاز به تغییر کد.
/// </summary>
public class AIConfidenceSettings
{
    [Key]
    public int AIConfidenceSettingsId { get; set; }

    public double HighThreshold { get; set; } = 0.85;

    public double MediumThreshold { get; set; } = 0.50;

    public DateTime ModifiedDate { get; set; } = DateTime.Now;
}
