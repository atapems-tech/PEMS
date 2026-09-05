using PEMS.Domain.Entities;

namespace PEMS.Persistence.Services;

/// <summary>
/// تطبیق رشتهٔ آزاد Document.DocumentType با کد رسمی سند (طبق همان الگوی DisciplineMatcher).
/// ترتیب تطبیق: Code دقیق -> یکی از ImportAliases -> نام کامل -> پیشوندی/شامل‌شدن.
/// </summary>
public static class DocumentTypeMatcher
{
    public static DocumentTypeCode? Match(IEnumerable<DocumentTypeCode> types, string? rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return null;

        var normalized = rawType.Trim().ToUpperInvariant();
        var list = types.ToList();

        var match = list.FirstOrDefault(t => t.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        match = list.FirstOrDefault(t =>
            !string.IsNullOrWhiteSpace(t.ImportAliases) &&
            t.ImportAliases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(alias => alias.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
        if (match != null) return match;

        match = list.FirstOrDefault(t => t.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        match = list.FirstOrDefault(t =>
            normalized.StartsWith(t.Code, StringComparison.OrdinalIgnoreCase) ||
            t.Name.ToUpperInvariant().Contains(normalized) ||
            normalized.Contains(t.Name.ToUpperInvariant()));

        return match;
    }
}
