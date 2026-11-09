using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using PEMS.Web.Configuration;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-540: on a Persian page the validation messages, the password-policy messages and many browser tab titles were English.
/// Identity messages come from <c>Id_*</c> keys, the default validation messages become resource keys, and every page title is a key with
/// a Persian value (the auth and embed layouts translate the title like _Layout does).
/// </summary>
public class PersianMessagesAndTitlesTests
{
    private static string Repo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Repo(), .. parts]));

    private static Dictionary<string, string> Resx(string file)
        => Regex.Matches(Read("PEMS.Web", "Resources", file), "<data name=\"([^\"]+)\"[^>]*>\\s*<value>([^<]*)</value>")
            .GroupBy(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value))
            .ToDictionary(g => g.Key, g => System.Net.WebUtility.HtmlDecode(g.First().Groups[2].Value));

    /// <summary>Shows which key and arguments the describer asked for.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, "fa:" + name);
        public LocalizedString this[string name, params object[] arguments] => new(name, "fa:" + name + ":" + string.Join(",", arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    [Fact]
    public void IdentityMessages_ComeFromTheResources_AndKeepTheirCodes()
    {
        var d = new LocalizedIdentityErrorDescriber(new KeyLocalizer());
        var tooShort = d.PasswordTooShort(8);
        Assert.Equal("PasswordTooShort", tooShort.Code);
        Assert.Equal("fa:Id_PasswordTooShort:8", tooShort.Description);
        Assert.Equal("fa:Id_DuplicateUserName:ali", d.DuplicateUserName("ali").Description);
        Assert.Equal("fa:Id_PasswordMismatch", d.PasswordMismatch().Description);
    }

    [Fact]
    public void EveryIdentityKey_HasAnEnglishAndAPersianValue_WithTheSamePlaceholders()
    {
        var en = Resx("SharedResource.resx");
        var fa = Resx("SharedResource.fa.resx");
        var keys = Regex.Matches(Read("PEMS.Web", "Security", "LocalizedIdentityErrorDescriber.cs"), "\"(Id_[A-Za-z]+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.True(keys.Count >= 22);
        foreach (var key in keys)
        {
            Assert.True(en.ContainsKey(key), "en " + key);
            Assert.True(fa.ContainsKey(key), "fa " + key);
            Assert.Equal(Regex.Matches(en[key], @"\{\d\}").Count, Regex.Matches(fa[key], @"\{\d\}").Count);
        }
    }

    [Fact]
    public void DefaultValidationMessages_BecomeKeys_ButOwnMessagesAreKept()
    {
        Assert.Equal("The {0} field is required.", ValidationMessageKeys.KeyFor(new RequiredAttribute()));
        Assert.Equal("The field {0} must be a string with a maximum length of {1}.", ValidationMessageKeys.KeyFor(new StringLengthAttribute(50)));
        Assert.Equal("The field {0} must be a string with a minimum length of {2} and a maximum length of {1}.", ValidationMessageKeys.KeyFor(new StringLengthAttribute(50) { MinimumLength = 2 }));

        var own = new RequiredAttribute { ErrorMessage = "Mine" };
        var plain = new RequiredAttribute();
        var context = new Microsoft.AspNetCore.Mvc.ModelBinding.Metadata.ValidationMetadataProviderContext(
            Microsoft.AspNetCore.Mvc.ModelBinding.Metadata.ModelMetadataIdentity.ForType(typeof(string)), Microsoft.AspNetCore.Mvc.ModelBinding.ModelAttributes.GetAttributesForType(typeof(string)));
        context.ValidationMetadata.ValidatorMetadata.Add(own);
        context.ValidationMetadata.ValidatorMetadata.Add(plain);
        new ValidationMessageKeys().CreateValidationMetadata(context);
        Assert.Equal("Mine", own.ErrorMessage);
        Assert.Equal("The {0} field is required.", plain.ErrorMessage);
    }

    [Fact]
    public void EveryValidationKey_HasAPersianValue_WithItsPlaceholder()
    {
        var fa = Resx("SharedResource.fa.resx");
        foreach (var key in new[] { "The {0} field is required.", "The {0} field is not a valid e-mail address.", "The field {0} must be between {1} and {2}.", "'{0}' and '{1}' do not match." })
        {
            Assert.True(fa.ContainsKey(key), key);
            Assert.Contains("{0}", fa[key]);
            Assert.DoesNotMatch("[A-Za-z]", fa[key]);
        }
    }

    [Fact]
    public void ProgramWires_TheDescriberAndTheValidationKeys()
    {
        var program = Read("PEMS.Web", "Program.cs");
        Assert.Contains(".AddErrorDescriber<PEMS.Web.Security.LocalizedIdentityErrorDescriber>()", program);
        Assert.Contains("options.ModelMetadataDetailsProviders.Add(new PEMS.Web.Configuration.ValidationMessageKeys())", program);
    }

    [Fact]
    public void EveryLiteralPageTitle_HasAPersianValue_AndNoTitleIsWrittenInPersian()
    {
        var fa = Resx("SharedResource.fa.resx");
        var web = Path.Combine(Repo(), "PEMS.Web");
        var titles = Directory.EnumerateFiles(web, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cshtml") || f.EndsWith(".cs")) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "ViewData\\[\"Title\"\\]\\s*=\\s*\"([^\"]*)\"").Select(m => (File: Path.GetFileName(f), Title: m.Groups[1].Value)))
            .ToList();
        Assert.True(titles.Count > 200);
        Assert.Empty(titles.Where(t => Regex.IsMatch(t.Title, "[\u0600-\u06FF]")).Select(t => t.File + ": " + t.Title));
        Assert.Empty(titles.Where(t => t.Title.Length > 0 && !fa.ContainsKey(t.Title)).Select(t => t.File + ": " + t.Title).Distinct());
    }

    [Theory]
    [InlineData("_AuthLayout.cshtml")]
    [InlineData("_LayoutEmbed.cshtml")]
    public void TheAuthAndEmbedLayouts_TranslateTheTitle(string layout)
        => Assert.Contains("Localizer[ViewData[\"Title\"]!.ToString()!].Value) - PEMS</title>", Read("PEMS.Web", "Views", "Shared", layout));

    [Fact]
    public void TheMessagesTheReviewFound_GoThroughTheLocalizer()
    {
        Assert.Contains("T(\"Equipment, Vendor and Project are required.\")", Read("PEMS.Web", "Controllers", "MDBController.cs"));
        Assert.Contains("T(\"Please select at least one document.\")", Read("PEMS.Web", "Controllers", "TransmittalController.cs"));
        Assert.Contains("T(\"A valid, active discipline is required to create an employee.\")", Read("PEMS.Web", "Areas", "Admin", "Controllers", "UsersController.cs"));
        Assert.Contains("@Localizer[\"This user has no linked employee", Read("PEMS.Web", "Areas", "Admin", "Views", "Users", "Edit.cshtml"));
    }
}
