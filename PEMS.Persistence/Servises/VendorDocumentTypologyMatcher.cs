using PEMS.Domain.Entities;

namespace PEMS.Persistence.Services;

/// <summary>
/// تطبیق کد/توضیح تایپولوژی مدرک فروشنده با جدول رسمی Appendix 3 (هم‌الگو با DocumentTypeMatcher).
/// </summary>
public static class VendorDocumentTypologyMatcher
{
    public static VendorDocumentTypologyCode? Match(IEnumerable<VendorDocumentTypologyCode> types, string? rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode)) return null;

        var normalized = rawCode.Trim().ToUpperInvariant();
        var list = types.ToList();

        var match = list.FirstOrDefault(t => t.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        match = list.FirstOrDefault(t => t.Description.ToUpperInvariant().Contains(normalized));
        return match;
    }
}
