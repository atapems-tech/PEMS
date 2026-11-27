using PEMS.UI.Abstractions;

namespace PEMS.UI.Components;

// SCRUM-841: the organization chart (the shell of the old Team/OrganizationChart view). The element carries the addresses and the texts; pems-org-chart.js reads the tree (GET {TreeUrl}?disciplineId=), the disciplines (GET {DisciplinesUrl}, a list
// of { value, label }) and draws the chart from the org-chart script templates. A page definition names the addresses, so no controller or view belongs to the chart.

/// <summary>The texts of the organization chart (inputs, ADR-0008); the defaults are the platform's two languages.</summary>
public sealed record OrgChartTexts
{
    private static TranslatableText T(string en, string fa) => TranslatableText.Of(("en", en), ("fa", fa));
    public TranslatableText Empty { get; init; } = T("No maintenance organization has been configured yet.", "هیچ ساختار سازمانی تعمیرونگهداری هنوز پیکربندی نشده.");
    public TranslatableText Error { get; init; } = T("Loading the organization chart failed. Try again.", "بارگذاری نمودار سازمانی با خطا مواجه شد. دوباره تلاش کنید.");
    public TranslatableText Vacant { get; init; } = T("Vacant", "خالی (Vacant)");
    public TranslatableText Positions { get; init; } = T("Positions", "سمت‌ها");
    public TranslatableText Members { get; init; } = T("Members", "اعضا");
    public TranslatableText ViewDetails { get; init; } = T("View team details", "مشاهده‌ی جزئیات تیم");
    public TranslatableText FilterEmpty { get; init; } = T("No position matches this filter.", "هیچ سمتی با این فیلتر وجود ندارد.");
    public TranslatableText NoDiscipline { get; init; } = T("No discipline found", "هیچ دیسیپلینی یافت نشد");
    public TranslatableText AllDisciplines { get; init; } = T("All disciplines", "همه‌ی دیسیپلین‌ها");
    public TranslatableText Back { get; init; } = T("Back to the teams", "بازگشت به تیم‌ها");
    public TranslatableText SearchDiscipline { get; init; } = T("Search discipline...", "جستجوی دیسیپلین...");
    public TranslatableText Discipline { get; init; } = T("Discipline", "دیسیپلین");
    public TranslatableText Toolbar { get; init; } = T("Organization chart tools", "ابزار نمودار سازمانی");
    public TranslatableText ExpandAll { get; init; } = T("Expand all", "باز کردن همه");
    public TranslatableText CollapseAll { get; init; } = T("Collapse all", "بستن همه");
    public TranslatableText ZoomOut { get; init; } = T("Zoom out", "کوچک‌نمایی");
    public TranslatableText ZoomIn { get; init; } = T("Zoom in", "بزرگ‌نمایی");
    public TranslatableText Fit { get; init; } = T("Fit to the page", "تناسب با صفحه");
    public TranslatableText SummaryTitle { get; init; } = T("Position occupancy", "خلاصه‌ی اشغال سمت‌ها");
    public TranslatableText ShowPositions { get; init; } = T("Show positions", "نمایش سمت‌ها");
    public TranslatableText All { get; init; } = T("All", "همه");
    public TranslatableText Filled { get; init; } = T("Filled", "تکمیل‌شده");
    public TranslatableText VacantShort { get; init; } = T("Vacant", "خالی");
    public TranslatableText TotalPositions { get; init; } = T("Total positions", "کل سمت‌ها");
    public TranslatableText Occupancy { get; init; } = T("Occupancy rate", "نرخ اشغال");
    public TranslatableText InactiveNote { get; init; } = T("Inactive positions are not counted.", "سمت‌های غیرفعال در شمارش نیامده‌اند.");
    public TranslatableText Loading { get; init; } = T("Loading the organization chart...", "در حال بارگذاری نمودار سازمانی...");
    public TranslatableText Retry { get; init; } = T("Try again", "تلاش دوباره");
    public TranslatableText ChartLabel { get; init; } = T("Maintenance organization chart", "نمودار سازمانی تعمیرونگهداری");
    public TranslatableText LegendTitle { get; init; } = T("Chart legend", "راهنمای نمودار");
    public TranslatableText Group { get; init; } = T("Maintenance group", "گروه نگهداری");
    public TranslatableText Team { get; init; } = T("Team", "تیم");
    public TranslatableText Shift { get; init; } = T("Shift / sub-team / unit", "شیفت / زیرتیم / واحد");
    public TranslatableText Occupied { get; init; } = T("Filled position", "سمت تکمیل‌شده");
    public TranslatableText VacantPosition { get; init; } = T("Vacant position", "سمت خالی");
    public TranslatableText NoSelection { get; init; } = T("Click a node to see its details.", "روی یک گره کلیک کنید تا جزئیاتش را ببینید.");
}

/// <summary>Inputs of <see cref="PemsOrgChart"/>.</summary>
public sealed record OrgChartOptions : FrameOptions
{
    public string Id { get; init; } = "pemsOrgChartRoot";
    /// <summary>The address of the JSON tree (a file-host source, <c>?disciplineId=</c> narrows it).</summary>
    public required string TreeUrl { get; init; }
    /// <summary>The address of the list of disciplines as <c>[{ value, label }]</c>; without it the discipline filter has no choices.</summary>
    public string? DisciplinesUrl { get; init; }
    /// <summary>The page of a team; the chart adds <c>?teamId=</c>.</summary>
    public string DetailsUrl { get; init; } = "/p/team-details";
    public string BackUrl { get; init; } = "/p/teams";
    public OrgChartTexts Texts { get; init; } = new();
}
