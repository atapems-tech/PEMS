using Microsoft.Extensions.Configuration;

namespace PEMS.Web.Services;

/// <summary>The letterhead of the company for the letter PDF; read from the section "Letterhead" of appsettings (default: PEMS).</summary>
public class LetterheadSettings
{
    public string CompanyName { get; set; } = "PEMS";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }

    public static LetterheadSettings From(IConfiguration configuration)
    {
        var s = new LetterheadSettings();
        var section = configuration.GetSection("Letterhead");
        if (!string.IsNullOrWhiteSpace(section["CompanyName"])) s.CompanyName = section["CompanyName"]!;
        s.Address = section["Address"];
        s.Phone = section["Phone"];
        s.Website = section["Website"];
        return s;
    }
}
