using PEMS.Domain.Entities;

namespace PEMS.Persistence.Services;

/// <summary>
/// تطبیق قوی‌تر کد دیسیپلین هنگام Import (طبق باگ واقعی گزارش‌شده: تطبیق دقیق قبلی، کدهای
/// کوتاه رایج در فایل‌های واقعی MDR/VDR مثل "ME"، "PR"، "EL" را پیدا نمی‌کرد و بی‌صدا شکست
/// می‌خورد). ترتیب تطبیق: Code دقیق -> یکی از ImportAliases -> پیشوند/شامل‌شدن در Name.
/// </summary>
public static class DisciplineMatcher
{
    public static Discipline? Match(IEnumerable<Discipline> disciplines, string? rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode)) return null;

        var normalized = rawCode.Trim().ToUpperInvariant();
        var list = disciplines.ToList();

        // ۱) تطبیق دقیق روی Code داخلی سیستم
        var match = list.FirstOrDefault(d => d.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // ۲) تطبیق روی یکی از کدهای جایگزین رایج (ImportAliases)
        match = list.FirstOrDefault(d =>
            !string.IsNullOrWhiteSpace(d.ImportAliases) &&
            d.ImportAliases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(alias => alias.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
        if (match != null) return match;

        // ۳) تطبیق روی نام کامل دیسیپلین (اگر اکسل به‌جای کد، نام کامل نوشته باشد)
        match = list.FirstOrDefault(d => d.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // ۴) آخرین تلاش: پیشوندی یا شامل‌شدن (مثلاً "MECH." یا "Mechanical Static")
        match = list.FirstOrDefault(d =>
            normalized.StartsWith(d.Code, StringComparison.OrdinalIgnoreCase) ||
            d.Name.ToUpperInvariant().Contains(normalized) ||
            normalized.Contains(d.Name.ToUpperInvariant()));

        return match;
    }
}
