using System.Runtime.CompilerServices;
using System.Xml.Linq;
using PEMS.Web.Helpers;

namespace PEMS.Tests.Web;

/// <summary>
/// بازطراحی UI مدارک مهندسی فقط نمایشی است: وضعیت ذخیره‌شده تغییر نمی‌کند و هر وضعیت
/// رنگ + آیکون + متن (نه فقط رنگ) دارد و کلیدهای EN/FA هم‌تراز هستند.
/// </summary>
public class EngineeringDocumentsUiTests
{
    private static readonly string[] Statuses =
        { "Draft", "Submitted", "IDC Review", "Checked", "Approved", "Issued", "Rejected", "Revise & Resubmit", "Client Approved", "Archived", "Superseded" };

    [Fact]
    public void EveryStatus_HasToneIconAndResourceKey()
    {
        foreach (var s in Statuses)
        {
            Assert.StartsWith("vs-pill--", DocumentStatusBadge.Tone(s));
            Assert.StartsWith("bi-", DocumentStatusBadge.Icon(s));
            Assert.StartsWith("St_", DocumentStatusBadge.ResourceKey(s));
        }
    }

    [Theory]
    [InlineData("Approved", "vs-pill--success")]
    [InlineData("Rejected", "vs-pill--danger")]
    [InlineData("Revise & Resubmit", "vs-pill--warning")]
    [InlineData("IDC Review", "vs-pill--info")]
    [InlineData("Draft", "vs-pill--neutral")]
    public void Tone_MapsSemantically(string status, string expected) => Assert.Equal(expected, DocumentStatusBadge.Tone(status));

    [Fact]
    public void ResourceKey_HandlesSpacesAmpersandAndEmpty()
    {
        Assert.Equal("St_IDCReview", DocumentStatusBadge.ResourceKey("IDC Review"));
        Assert.Equal("St_ReviseResubmit", DocumentStatusBadge.ResourceKey("Revise & Resubmit"));
        Assert.Equal("St_Draft", DocumentStatusBadge.ResourceKey(null));
    }

    [Fact]
    public void EdAndStKeys_ExistInBothLanguages_WithNonEmptyValues()
    {
        var dir = Path.Combine(Path.GetDirectoryName(Here())!, "..", "..", "PEMS.Web", "Resources");
        static Dictionary<string, string> Load(string f) => XDocument.Load(f).Root!.Elements("data")
            .ToDictionary(e => (string)e.Attribute("name")!, e => (string?)e.Element("value") ?? "", StringComparer.Ordinal);
        var en = Load(Path.Combine(dir, "SharedResource.resx"));
        var fa = Load(Path.Combine(dir, "SharedResource.fa.resx"));
        var keys = en.Keys.Concat(fa.Keys).Where(k => k.StartsWith("Ed_") || k.StartsWith("St_")).Distinct().ToList();
        Assert.NotEmpty(keys);
        foreach (var k in keys)
        {
            Assert.True(en.TryGetValue(k, out var e) && e.Length > 0, $"EN missing {k}");
            Assert.True(fa.TryGetValue(k, out var f) && f.Length > 0, $"FA missing {k}");
        }
    }

    private static string Here([CallerFilePath] string p = "") => p;
}
