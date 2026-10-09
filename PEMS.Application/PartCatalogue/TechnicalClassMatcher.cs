using System.Text;
using System.Text.RegularExpressions;

namespace PEMS.Application.PartCatalogue;

/// <summary>یک Technical Class مرجع (فقط فیلدهایی که تطبیق لازم دارد).</summary>
public sealed record TechnicalClassRef(int Id, string Code, string Description);

public enum TechnicalClassResolutionStatus { None, Resolved, Ambiguous }

/// <summary>
/// نتیجه‌ی تطبیق متن آزاد با Technical Class. <c>Reason</c> فقط توضیح قاعده‌ی اعمال‌شده است (درصد اطمینان ساخته نمی‌شود):
/// Code | NamePrefix | CodeAmbiguous | CodeNameConflict | NameAmbiguous | PartialChild | Keywords | None.
/// </summary>
public sealed class TechnicalClassResolution
{
    public TechnicalClassResolutionStatus Status { get; init; }
    public string Reason { get; init; } = "None";
    public TechnicalClassRef? Resolved { get; init; }
    public IReadOnlyList<TechnicalClassRef> Candidates { get; init; } = Array.Empty<TechnicalClassRef>();
}

/// <summary>
/// تطبیق قطعی (بدون حدس و بدون امتیاز اطمینان ساختگی) متن FREE FORMAT TEXT با فایل مرجع «TECHNICAL CLASSESS-2026-08-29»:
/// ستون‌های مرجع = Technical Class (کد: IR##### یا عددی)، Description (نام استاندارد «NOUN,MODIFIER»)، Long Description.
/// قواعد به ترتیب:
/// ۱) کد صریح: توکن IR\d+ در هر جای متن، یا هر کد مرجع (عددی/حرفی-عددی) داخل پرانتز «NAME(CODE)» (قالب فرم اکسل: ELBOW GENERIC(IR25749)).
///    کدهای عددی/حرفی بدون پرانتز عمداً نادیده گرفته می‌شوند (با ابعاد/اعداد متن اشتباه می‌شوند). یک کد معتبر ⇒ Resolved؛ چند کد ⇒ Ambiguous؛
///    کدی که با نامِ ابتدای متن (کلاس دیگر) تعارض دارد ⇒ Ambiguous با دلیل CodeNameConflict.
/// ۲) نام در ابتدای متن (Prefix با مرز واژه؛ ویرگول ≡ فاصله): طولانی‌ترین Description که ابتدای متن است. یکتا ⇒ Resolved؛ Descriptionهای تکراری ⇒ Ambiguous.
///    محافظ «PartialChild»: اگر متن بعد از نام یک زیرکلاس را نیمه‌کاره نشان دهد (PIPE,BENT ...) به‌جای حدس، Ambiguous.
/// ۳) کلیدواژه (همه‌ی توکن‌های یک Description چندکلمه‌ای در متن): فقط Candidate (هرگز انتخاب خودکار).
/// ۴) غیر این صورت ⇒ None.
/// نرمال‌سازی: حروف بزرگ، یکسان‌سازی فاصله/ویرگول (فارسی «،» هم)، ارقام فارسی/عربی → لاتین، حذف ZWNJ/کاراکترهای جهت.
/// </summary>
public sealed class TechnicalClassMatcher
{
    public const int MaxTextLength = 1000;
    public const int MaxCandidates = 10;

    private static readonly Regex IrCode = new(@"(?<![A-Z0-9])IR\d{3,}(?![A-Z0-9])", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    // کد داخل پرانتز «NAME(CODE)»: هر توکن دقیقاً برابر یکی از کدهای مرجع (کدهای مرجع: IR#####، عددی، و ~۲۶۳ کد حرفی-عددی مثل L-OBJ-02)
    private static readonly Regex ParenCode = new(@"\(([A-Z0-9][A-Z0-9._\-]{2,29})\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Tokens = new(@"[A-Z0-9]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private sealed record Entry(TechnicalClassRef Ref, string NormDesc, string FlatDesc, string[] Tokens);

    private readonly Dictionary<string, TechnicalClassRef> _byCode;
    private readonly List<Entry> _entries;

    public TechnicalClassMatcher(IEnumerable<TechnicalClassRef> activeClasses)
    {
        _byCode = new Dictionary<string, TechnicalClassRef>(StringComparer.Ordinal);
        _entries = new List<Entry>();
        foreach (var c in activeClasses)
        {
            var code = Normalize(c.Code);
            if (code.Length > 0 && !_byCode.ContainsKey(code)) _byCode[code] = c;
            var nd = Normalize(c.Description);
            if (nd.Length > 0) _entries.Add(new Entry(c, nd, Flat(nd), Tokens.Matches(nd).Select(m => m.Value).ToArray()));
        }
    }

    public static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (var raw in s)
        {
            var ch = raw;
            if (ch >= '۰' && ch <= '۹') ch = (char)('0' + (ch - '۰'));
            else if (ch >= '٠' && ch <= '٩') ch = (char)('0' + (ch - '٠'));
            else if (ch == '،' || ch == '，') ch = ',';
            else if (ch is '‌' or '‍' or '‎' or '‏' or '­') continue;
            else if (ch == 'ي') ch = 'ی';
            else if (ch == 'ك') ch = 'ک';
            else if (char.IsWhiteSpace(ch)) ch = ' ';
            sb.Append(char.ToUpperInvariant(ch));
        }
        var t = Regex.Replace(sb.ToString(), @"\s*,\s*", ",");
        return Regex.Replace(t, @"\s+", " ").Trim();
    }

    /// <summary>ویرگول و فاصله هم‌ارز می‌شوند (مرجع «ELBOW,GENERIC» می‌نویسد، کاربر «ELBOW GENERIC»).</summary>
    private static string Flat(string normalized) => Regex.Replace(normalized.Replace(',', ' '), @"\s+", " ").Trim();

    public TechnicalClassResolution Resolve(string? freeFormatText)
    {
        var text = Normalize(freeFormatText);
        if (text.Length == 0) return None();
        if (text.Length > MaxTextLength) text = text[..MaxTextLength];

        // ۱) کد صریح
        var found = new List<TechnicalClassRef>();
        foreach (var m in IrCode.Matches(text).Cast<Match>().Select(x => (x.Index, Code: x.Value))
                     .Concat(ParenCode.Matches(text).Cast<Match>().Select(x => (x.Index, Code: x.Groups[1].Value)))
                     .OrderBy(x => x.Index))
            if (_byCode.TryGetValue(m.Code, out var hit) && found.All(f => f.Id != hit.Id)) found.Add(hit);

        // نام در ابتدای متن (طولانی‌ترین Description با مرز واژه؛ ویرگول ≡ فاصله). اگر چند کلاس فقط در جداکننده فرق دارند
        // («PUMP,CENTRIFUGAL» و «PUMP CENTRIFUGAL») جداکننده‌ی دقیقِ نوشته‌شده تصمیم می‌گیرد، وگرنه انتخاب صریح کاربر.
        var flatText = Flat(text);
        var flatPrefix = _entries.Where(e => StartsWithWord(flatText, e.FlatDesc)).ToList();
        List<Entry>? nameGroup = null;
        List<Entry>? nameAmbiguous = null;
        if (flatPrefix.Count > 0)
        {
            var longest = flatPrefix.Max(e => e.FlatDesc.Length);
            var f = flatPrefix.Where(e => e.FlatDesc.Length == longest).ToList();
            if (f.Count == 1) nameGroup = f;
            else
            {
                var exact = f.Where(e => StartsWithWord(text, e.NormDesc)).ToList();
                if (exact.Count == 1) nameGroup = exact; else nameAmbiguous = f;
            }
        }

        if (found.Count > 1) return Ambiguous("CodeAmbiguous", found);
        if (found.Count == 1)
        {
            // کد و نامِ ابتدای متن باید با هم سازگار باشند؛ تعارض ⇒ انتخاب صریح کاربر (نه برنده‌ی خودکار)
            var named = nameGroup ?? nameAmbiguous;
            if (named != null && named.All(e => e.Ref.Id != found[0].Id))
                return Ambiguous("CodeNameConflict", new[] { found[0] }.Concat(named.Select(e => e.Ref)).ToList());
            return new TechnicalClassResolution { Status = TechnicalClassResolutionStatus.Resolved, Reason = "Code", Resolved = found[0] };
        }

        // ۲) نام
        if (nameAmbiguous != null) return Ambiguous("NameAmbiguous", nameAmbiguous.Select(e => e.Ref).ToList());
        if (nameGroup != null)
        {
            var matched = nameGroup[0];
            var children = PartialChildren(flatText, matched);
            if (children.Count > 0) return Ambiguous("PartialChild", new[] { matched.Ref }.Concat(children).ToList());
            return new TechnicalClassResolution { Status = TechnicalClassResolutionStatus.Resolved, Reason = "NamePrefix", Resolved = matched.Ref };
        }

        // ۳) کلیدواژه: فقط پیشنهاد، هرگز انتخاب خودکار
        var textTokens = new HashSet<string>(Tokens.Matches(text).Select(m => m.Value), StringComparer.Ordinal);
        var keyword = _entries.Where(e => e.Tokens.Length >= 2 && e.Tokens.All(textTokens.Contains))
            .OrderByDescending(e => e.Tokens.Length).ThenByDescending(e => e.NormDesc.Length).ThenBy(e => e.Ref.Code, StringComparer.Ordinal)
            .Select(e => e.Ref).Take(MaxCandidates).ToList();
        return keyword.Count > 0 ? Ambiguous("Keywords", keyword) : None();
    }

    /// <summary>متن با Description شروع شود و کاراکتر بعد از آن حرف/رقم نباشد (مرز واژه).</summary>
    private static bool StartsWithWord(string text, string desc)
    {
        if (!text.StartsWith(desc, StringComparison.Ordinal)) return false;
        return text.Length == desc.Length || !char.IsLetterOrDigit(text[desc.Length]);
    }

    /// <summary>زیرکلاس‌های «DESC xxx» (شکل Flat) که واژه‌ی اول بخش بعدی متن با بخش بعدیشان یکی است ولی نام کاملشان در متن نیامده.</summary>
    private List<TechnicalClassRef> PartialChildren(string flatText, Entry matched)
    {
        var rest = flatText[matched.FlatDesc.Length..];
        if (!rest.StartsWith(' ')) return new List<TechnicalClassRef>();
        var firstWord = Tokens.Match(rest).Value;
        if (firstWord.Length == 0) return new List<TechnicalClassRef>();
        var childPrefix = matched.FlatDesc + " ";
        return _entries.Where(e => e.FlatDesc.StartsWith(childPrefix, StringComparison.Ordinal)
                                   && Tokens.Match(e.FlatDesc[childPrefix.Length..]).Value == firstWord)
            .OrderBy(e => e.NormDesc, StringComparer.Ordinal).Select(e => e.Ref).Take(MaxCandidates - 1).ToList();
    }

    private static TechnicalClassResolution None() => new() { Status = TechnicalClassResolutionStatus.None, Reason = "None" };

    private static TechnicalClassResolution Ambiguous(string reason, IReadOnlyList<TechnicalClassRef> candidates) =>
        new() { Status = TechnicalClassResolutionStatus.Ambiguous, Reason = reason, Candidates = candidates.Take(MaxCandidates).ToList() };
}
