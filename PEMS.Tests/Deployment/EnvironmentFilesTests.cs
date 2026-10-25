using System.Text.RegularExpressions;
using PEMS.Shared.Configuration;

namespace PEMS.Tests.Deployment;

/// <summary>
/// Environment files: the loader format and the separation of settings (committed) from secrets (git-ignored).
/// </summary>
public class EnvironmentFilesTests
{
    private static readonly string[] Environments = { "local", "docker", "demo", "staging", "uat", "production-kangan" };

    private static string EnvDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "env");
    }

    [Fact]
    public void Parse_handles_comments_export_quotes_and_section_separators()
    {
        var values = EnvFiles.Parse(new[]
        {
            "# comment", "", "Ollama__Url=http://x:1", "export Fido2__Origins__0 = \"https://a\"",
            "ConnectionStrings__PEMSConnection=Server=s;Password=p#1;Encrypt=True", "Empty=", "'quoted'=ignored-key-is-kept", "no-equals-line"
        });

        Assert.Equal("http://x:1", values["Ollama:Url"]);
        Assert.Equal("https://a", values["Fido2:Origins:0"]);
        Assert.Equal("Server=s;Password=p#1;Encrypt=True", values["ConnectionStrings:PEMSConnection"]);   // '#' and '=' inside a value survive
        Assert.Equal("", values["Empty"]);
        Assert.False(values.ContainsKey("no-equals-line"));
    }

    [Theory]
    [InlineData(null, "Development", "local")]
    [InlineData(null, "UAT", "uat")]
    [InlineData(null, "Production", null)]
    [InlineData("demo", "Production", "demo")]
    [InlineData("production-kangan", "Development", "production-kangan")]
    public void ResolveName_prefers_PEMS_ENV_then_maps_development_and_uat(string? explicitName, string host, string? expected)
        => Assert.Equal(expected, EnvFiles.ResolveName(explicitName, host));

    [Fact]
    public void Load_merges_secrets_over_settings_and_requires_the_settings_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pems-env-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Throws<FileNotFoundException>(() => EnvFiles.Load("demo", dir));

            File.WriteAllText(Path.Combine(dir, "demo.env"), "A=1\nB=settings\n");
            var withoutSecrets = EnvFiles.Load("demo", dir);
            Assert.False(withoutSecrets.SecretsFileFound);
            Assert.Equal("settings", withoutSecrets.Values["B"]);

            File.WriteAllText(Path.Combine(dir, "demo.secrets.env"), "B=secret\nC=3\n");
            var both = EnvFiles.Load("demo", dir);
            Assert.True(both.SecretsFileFound);
            Assert.Equal("1", both.Values["A"]);
            Assert.Equal("secret", both.Values["B"]);
            Assert.Equal("3", both.Values["C"]);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Every_environment_defines_exactly_the_same_keys()
    {
        var keySets = Environments.ToDictionary(
            e => e, e => EnvFiles.Parse(File.ReadAllLines(Path.Combine(EnvDir(), e + ".env"))).Keys.OrderBy(k => k).ToList());
        var reference = keySets["local"];
        foreach (var (name, keys) in keySets)
            Assert.True(reference.SequenceEqual(keys), $"{name}.env has different keys than local.env");
    }

    [Fact]
    public void Settings_files_hold_no_secrets_and_secrets_stay_in_the_secrets_files()
    {
        var secretLike = new Regex("password|secret|token|apikey|connectionstrings|username", RegexOptions.IgnoreCase);
        foreach (var e in Environments)
        {
            var settings = EnvFiles.Parse(File.ReadAllLines(Path.Combine(EnvDir(), e + ".env")));
            // password-POLICY and the admin user NAME are plain settings; every other secret-looking key belongs in the secrets file
            var allowed = new[] { "Authentication:Password:", "Seed:Admin:UserName" };
            Assert.DoesNotContain(settings.Keys, k => secretLike.IsMatch(k) && !allowed.Any(p => k.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
            Assert.DoesNotMatch(new Regex(@"(password|pwd)\s*=", RegexOptions.IgnoreCase), File.ReadAllText(Path.Combine(EnvDir(), e + ".env")));

            var template = EnvFiles.Parse(File.ReadAllLines(Path.Combine(EnvDir(), e + ".secrets.env.example")));
            Assert.True(template.ContainsKey("ConnectionStrings:PEMSConnection"), $"{e} secrets template lacks the connection string");
            Assert.True(template.ContainsKey("Seed:Admin:Password"), $"{e} secrets template lacks the admin password");
        }
    }

    [Fact]
    public void No_real_secrets_file_is_committed()
    {
        var gitignore = File.ReadAllText(Path.Combine(Path.GetDirectoryName(EnvDir())!, ".gitignore"));
        Assert.Contains("env/*.secrets.env", gitignore);
        Assert.Contains("env/*.secrets.env", File.ReadAllText(Path.Combine(Path.GetDirectoryName(EnvDir())!, ".dockerignore")));
    }
}
