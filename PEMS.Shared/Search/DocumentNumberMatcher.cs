using System.Text.RegularExpressions;

namespace PEMS.Shared.Search;

/// <summary>
/// ==============================================================================
/// تطبیق هوشمند شماره‌ی مدرک - طبق مشکل واقعی گزارش‌شده: شماره‌ی P&amp;ID ثبت‌شده روی تجهیز
/// گاهی پسوند اضافه دارد (مثلاً شماره‌ی رویژن در انتها: "PID-1005-Rev2" یا "PID-1005-02")
/// در حالی که DocumentNo واقعی مدرک بدون آن پسوند است ("PID-1005") - چون رویژن‌ها جدا در
/// DocumentRevision نگه‌داری می‌شوند، نه در خود شماره‌ی مدرک. تطبیق کامل (==) این موارد را
/// از دست می‌داد.
///
/// استراتژی سه‌لایه (از دقیق‌ترین به محتاط‌ترین):
///   ۱. تطبیق کامل
///   ۲. تطبیق بعد از حذف پسوند صریح رویژن (فقط وقتی کلمه‌ی Rev/Revision واقعاً وجود دارد -
///      برای جلوگیری از خرابکاری روی شماره‌مدرک‌هایی که خودشان طبیعتاً به عدد/حرف ختم می‌شوند)
///   ۳. تطبیق ابتدایی دوطرفه (یکی پیشوند دیگری) با اولویت بیشترین شباهت - برای پسوندهای
///      غیراستاندارد (بدون کلمه‌ی Rev)
/// ==============================================================================
/// </summary>
public static class DocumentNumberMatcher
{
    private static readonly Regex RevisionKeywordSuffix = new(
        @"[\s\-_/]*REV(?:ISION)?\.?\s*[A-Z0-9]{1,3}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// حذف پسوند صریح رویژن از انتهای شماره (فقط وقتی کلمه‌ی Rev/Revision واقعاً وجود دارد).
    /// </summary>
    public static string StripRevisionKeyword(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw ?? "";

        var cleaned = RevisionKeywordSuffix.Replace(raw.Trim(), "").TrimEnd('-', '_', '/', ' ');
        return string.IsNullOrWhiteSpace(cleaned) ? raw.Trim() : cleaned;
    }

    /// <summary>
    /// چک دوتایی ساده (بدون انتخاب از میان چند نامزد) - برای وقتی باید همه‌ی نامزدهای
    /// منطبق نگه داشته شوند (مثلاً همه‌ی تجهیزاتی که یک مدرک را P&amp;ID خود می‌دانند)، نه
    /// فقط یک «بهترین» مورد.
    /// </summary>
    public static bool IsMatch(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;

        var an = a.Trim();
        var bn = b.Trim();

        if (string.Equals(an, bn, StringComparison.OrdinalIgnoreCase)) return true;

        var aStripped = StripRevisionKeyword(an);
        var bStripped = StripRevisionKeyword(bn);
        if (string.Equals(aStripped, bStripped, StringComparison.OrdinalIgnoreCase)) return true;

        return an.StartsWith(bn, StringComparison.OrdinalIgnoreCase) || bn.StartsWith(an, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// بهترین تطبیق را از میان نامزدها پیدا می‌کند - یا null اگر هیچ‌کدام حتی در لایه‌ی
    /// محتاط‌ترین (تطبیق ابتدایی) هم منطبق نبودند.
    /// </summary>
    public static T? FindBestMatch<T>(IReadOnlyList<T> candidates, string? target, Func<T, string?> numberSelector)
        where T : class
    {
        if (candidates.Count == 0 || string.IsNullOrWhiteSpace(target)) return null;

        var t = target.Trim();

        // لایه ۱: تطبیق کامل
        var exact = candidates.FirstOrDefault(c =>
            string.Equals(numberSelector(c)?.Trim(), t, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // لایه ۲: بعد از حذف پسوند صریح رویژن
        var tStripped = StripRevisionKeyword(t);
        if (!string.Equals(tStripped, t, StringComparison.OrdinalIgnoreCase))
        {
            var strippedMatch = candidates.FirstOrDefault(c =>
                string.Equals(StripRevisionKeyword(numberSelector(c)), tStripped, StringComparison.OrdinalIgnoreCase));
            if (strippedMatch != null) return strippedMatch;
        }

        // لایه ۳: تطبیق ابتدایی دوطرفه - با اولویت بیشترین شباهت (برای وقتی چند نامزد
        // هم‌زمان این شرط را دارند، مثلاً "PID-100" و "PID-1005" هر دو با "PID-100" شروع می‌شوند)
        var prefixMatches = candidates
            .Where(c =>
            {
                var cn = numberSelector(c)?.Trim();
                if (string.IsNullOrWhiteSpace(cn)) return false;
                return cn.StartsWith(t, StringComparison.OrdinalIgnoreCase) || t.StartsWith(cn, StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(c => SearchScoring.ComputeFieldScore(numberSelector(c), t))
            .ToList();

        return prefixMatches.FirstOrDefault();
    }
}
