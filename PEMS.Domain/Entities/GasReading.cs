using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10d: یک قرائت پارامتر گاز در یک GasTest - چند قرائت (O2/LEL/H2S/CO/Other) با هم
/// زیرمجموعه‌ی یک GasTest اتمی ثبت می‌شوند. Result یک‌بار در لحظه‌ی ثبت محاسبه و منجمد می‌شود -
/// اگر بعداً GasParameterLimit تغییر کند، این Result تاریخی به‌صورت خاموش تغییر نمی‌کند (طبق
/// نیاز Immutability رکورد ایمنی‌محور در پلن - انحراف عمدی از الگوی Compute-on-read
/// InspectionRecord/Instrument).
/// </summary>
public class GasReading : BaseEntity
{
    public int GasReadingId { get; set; }

    public int GasTestId { get; set; }
    public GasTest? GasTest { get; set; }

    // O2, LEL, H2S, CO, Other - رشته‌ی ساده + اعتبارسنجی سمت سرور، همون الگوی EnergyType/PermitType.
    public string Parameter { get; set; } = string.Empty;

    public decimal Value { get; set; }

    // مثال: %VOL, %LEL, PPM - همیشه صریح ذخیره می‌شود، هرگز از برچسب UI استنتاج نمی‌شود.
    public string Unit { get; set; } = string.Empty;

    // Pass, Fail - یک‌بار در ثبت محاسبه و منجمد می‌شود.
    public string Result { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
