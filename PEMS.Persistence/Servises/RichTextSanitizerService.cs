using System.Text.RegularExpressions;
using Ganss.Xss;
using PEMS.Application.Interfaces;

namespace PEMS.Persistence.Services;

/// <summary>
/// پیاده‌سازی پاک‌سازی HTML با HtmlSanitizer (کتابخانه‌ی Ganss.Xss) - تگ‌ها و ویژگی‌های
/// موردنیاز ادیتور متن غنی (Bold/Italic/Underline/Font/Size/Color/Alignment/RTL-LTR/Lists/
/// Table/Link) مجاز هستند؛ هر اسکریپت یا رویداد جاوااسکریپت حذف می‌شود.
/// </summary>
public class RichTextSanitizerService : IRichTextSanitizerService
{
    private readonly HtmlSanitizer _sanitizer;

    public RichTextSanitizerService()
    {
        _sanitizer = new HtmlSanitizer();

        _sanitizer.AllowedTags.Clear();
        foreach (var tag in new[]
        {
            "p", "br", "b", "strong", "i", "em", "u", "s", "strike", "span", "div",
            "ul", "ol", "li", "a", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote",
            "table", "thead", "tbody", "tr", "td", "th", "font", "hr", "sub", "sup", "img"
        })
        {
            _sanitizer.AllowedTags.Add(tag);
        }

        _sanitizer.AllowedAttributes.Clear();
        foreach (var attr in new[] { "style", "class", "href", "target", "dir", "align", "color", "size", "face", "colspan", "rowspan", "src", "alt", "width", "height" })
        {
            _sanitizer.AllowedAttributes.Add(attr);
        }

        _sanitizer.AllowedCssProperties.Clear();
        foreach (var css in new[]
        {
            "color", "background-color", "font-size", "font-family", "font-weight", "font-style",
            "text-align", "text-decoration", "direction", "margin", "padding"
        })
        {
            _sanitizer.AllowedCssProperties.Add(css);
        }
    }

    public string Sanitize(string? html)
    {
        return string.IsNullOrWhiteSpace(html) ? string.Empty : _sanitizer.Sanitize(html);
    }

    public string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var noTags = Regex.Replace(html, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        noTags = Regex.Replace(noTags, "</p>|</div>|</li>", "\n", RegexOptions.IgnoreCase);
        noTags = Regex.Replace(noTags, "<[^>]+>", string.Empty);
        noTags = System.Net.WebUtility.HtmlDecode(noTags);

        return noTags.Trim();
    }
}
