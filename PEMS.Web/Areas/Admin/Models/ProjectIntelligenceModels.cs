namespace PEMS.Web.Areas.Admin.Models;

/// <summary>
/// مدل خواندن خروجی scripts/project-intelligence/Generate-ProjectIntelligence.ps1
/// (graphify-out/project-intelligence.json). این صرفاً یک DTO خواندنی است - خودِ محاسبه
/// و جمع‌آوری شواهد در اسکریپت PowerShell انجام می‌شود، نه این‌جا؛ این صفحه فقط نمایش می‌دهد.
/// </summary>
public class ProjectIntelligenceReport
{
    public DateTime? GeneratedAtUtc { get; set; }
    public string? Branch { get; set; }
    public string? LastCommit { get; set; }
    public int TotalCommits { get; set; }
    public int ModuleCount { get; set; }
    public int StubShimCount { get; set; }
    public int NoUiCount { get; set; }
    public int NotInNavCount { get; set; }
    public int NoTestCount { get; set; }
    public List<ProjectIntelligenceModule> Modules { get; set; } = new();
}

public class ProjectIntelligenceModule
{
    public string Module { get; set; } = "";
    public string Area { get; set; } = "";
    public string ControllerPath { get; set; } = "";
    public int ActionCount { get; set; }
    public int PostActionCount { get; set; }
    public bool IsRedirectShim { get; set; }
    public List<string> InjectedServices { get; set; } = new();
    public string ViewsPath { get; set; } = "";
    public int ViewCount { get; set; }
    public bool InSidebarNav { get; set; }
    public bool InTests { get; set; }
    public bool InDocs { get; set; }
    public string? GitLastTouched { get; set; }
    public int GitCommitCount { get; set; }
    public int GraphConnectivity { get; set; }
    public Dictionary<string, string> Evidence { get; set; } = new();
    public int EvidencePresent { get; set; }
    public int EvidenceUnknown { get; set; }
    public int EvidenceTotal { get; set; }
    public int EvidenceCoveragePct { get; set; }

    public string DisplayName => string.IsNullOrEmpty(Area) ? Module : $"{Module} ({Area})";
}
