using PEMS.UI.Rendering;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.Components;

namespace PEMS.UI.TagHelpers;

/// <summary>The Bootstrap classes of the menu, tab and breadcrumb frames, computed from the inputs. Plain C#, unit-testable without rendering.</summary>
public static class FrameState
{
    private static string With(string css, string? extra) => string.IsNullOrWhiteSpace(extra) ? css : css + " " + extra.Trim();

    public static string DropdownMenu(bool end, string? extra) => With(end ? "dropdown-menu dropdown-menu-end" : "dropdown-menu", extra);

    /// <summary>kind is tabs (default) or pills.</summary>
    public static string Nav(string? kind, string? extra) => With(string.Equals(kind, "pills", StringComparison.OrdinalIgnoreCase) ? "nav nav-pills" : "nav nav-tabs", extra);

    public static string Breadcrumb(string? extra) => With("breadcrumb", extra);

    public static string ListGroupItem(bool action, bool active, string? extra) =>
        With("list-group-item" + (action ? " list-group-item-action" : "") + (active ? " active" : ""), extra);

    /// <summary>tone: success, danger, primary, warning, info, secondary, dark, light (the Bootstrap text-bg color of the toast).</summary>
    public static string Toast(string tone, string? extra) => With("toast align-items-center text-bg-" + tone.ToLowerInvariant() + " border-0 show", extra);

    /// <summary>The table element: <c>table</c>, then <c>table-enterprise</c> and <c>table-sm</c> in that order when asked, then the page classes.</summary>
    public static string Table(bool enterprise, bool small, string? extra) => With("table" + (enterprise ? " table-enterprise" : "") + (small ? " table-sm" : ""), extra);
}

/// <summary>
/// MVC facade for the dropdown panel: <c>&lt;pems-dropdown-menu end="true" css="p-0"&gt;&lt;li&gt;...&lt;/pems-dropdown-menu&gt;</c> writes <c>&lt;ul class="dropdown-menu dropdown-menu-end p-0"&gt;</c>
/// (<c>as="div"</c> for a panel of free markup). Every other attribute and the content stay as the page wrote them.
/// </summary>
[HtmlTargetElement("pems-dropdown-menu")]
[PemsFacade(FacadeKind.Direct)]
public sealed class PemsDropdownMenuTagHelper : TagHelper
{
    [HtmlAttributeName("end")] public bool End { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }
    /// <summary>ul (default) or div.</summary>
    [HtmlAttributeName("as")] public string As { get; set; } = "ul";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = As;
        output.TagMode = TagMode.StartTagAndEndTag;
        ControlTagHelper.PutClassFirst(output, FrameState.DropdownMenu(End, Css));
    }
}

/// <summary>MVC facade for the tab strip: <c>&lt;pems-nav kind="tabs" css="mb-3" id="docTabs" role="tablist"&gt;&lt;li class="nav-item"&gt;...&lt;/pems-nav&gt;</c>; kind is tabs (default) or pills.</summary>
[HtmlTargetElement("pems-nav")]
[PemsFacade(FacadeKind.Direct)]
public sealed class PemsNavTagHelper : TagHelper
{
    [HtmlAttributeName("kind")] public string Kind { get; set; } = "tabs";
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "ul";
        output.TagMode = TagMode.StartTagAndEndTag;
        ControlTagHelper.PutClassFirst(output, FrameState.Nav(Kind, Css));
    }
}

/// <summary>MVC facade for the breadcrumb trail frame: <c>&lt;pems-crumb-trail css="mb-0 small"&gt;&lt;li class="breadcrumb-item"&gt;...&lt;/pems-crumb-trail&gt;</c> writes an <c>ol.breadcrumb</c>.</summary>
[HtmlTargetElement("pems-crumb-trail")]
[PemsFacade(FacadeKind.Direct)]
public sealed class PemsBreadcrumbTagHelper : TagHelper
{
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "ol";
        output.TagMode = TagMode.StartTagAndEndTag;
        ControlTagHelper.PutClassFirst(output, FrameState.Breadcrumb(Css));
    }
}

/// <summary>
/// MVC facade for the table element: <c>&lt;pems-table kind="enterprise" scale="small" css="mb-0" id="items"&gt;thead and tbody&lt;/pems-table&gt;</c> writes
/// <c>&lt;table class="table table-enterprise table-sm mb-0"&gt;</c>. Rows stay page markup; every other attribute (id, data-*) is kept.
/// A record list uses the pems-list family instead.
/// </summary>
[HtmlTargetElement("pems-table")]
[PemsFacade(FacadeKind.Direct)]
public sealed class PemsTableFrameTagHelper : TagHelper
{
    /// <summary>enterprise adds table-enterprise; empty for the plain Bootstrap table.</summary>
    [HtmlAttributeName("kind")] public string? Kind { get; set; }
    /// <summary>small adds table-sm.</summary>
    [HtmlAttributeName("scale")] public string? Scale { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "table";
        output.TagMode = TagMode.StartTagAndEndTag;
        ControlTagHelper.PutClassFirst(output, FrameState.Table(string.Equals(Kind, "enterprise", StringComparison.OrdinalIgnoreCase), string.Equals(Scale, "small", StringComparison.OrdinalIgnoreCase), Css));
    }
}

/// <summary>
/// MVC facade for a list-group row: <c>&lt;pems-list-group-item action="true" active="@(selected)" asp-action="Index"&gt;text&lt;/pems-list-group-item&gt;</c> writes
/// <c>&lt;a class="list-group-item list-group-item-action active"&gt;</c> (<c>as="div"</c> or <c>as="button"</c> for rows that are not links). The routing attributes of the anchor helper work.
/// </summary>
[HtmlTargetElement("pems-list-group-item")]
[PemsFacade(FacadeKind.Direct)]
public sealed class PemsListGroupItemTagHelper(IHtmlGenerator generator) : AnchorTagHelper(generator)
{
    [HtmlAttributeName("action")] public bool IsAction { get; set; }
    [HtmlAttributeName("active")] public bool Active { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }
    /// <summary>a (default), div or button.</summary>
    [HtmlAttributeName("as")] public string As { get; set; } = "a";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Action is not null || Controller is not null || Area is not null || Page is not null || PageHandler is not null || Route is not null || Host is not null || Protocol is not null || Fragment is not null)
            base.Process(context, output);
        output.TagName = As;
        output.TagMode = TagMode.StartTagAndEndTag;
        ControlTagHelper.PutClassFirst(output, FrameState.ListGroupItem(IsAction, Active, Css));
    }
}

/// <summary>MVC facade for the toast frame: <c>&lt;pems-toast tone="success" role="alert" data-bs-delay="5000"&gt;body and close button&lt;/pems-toast&gt;</c>.</summary>
[HtmlTargetElement("pems-toast")]
[PemsFacade(FacadeKind.Direct)]
public sealed class PemsToastTagHelper : TagHelper
{
    [HtmlAttributeName("tone")] public string Tone { get; set; } = "primary";
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        ControlTagHelper.PutClassFirst(output, FrameState.Toast(Tone, Css));
    }
}
