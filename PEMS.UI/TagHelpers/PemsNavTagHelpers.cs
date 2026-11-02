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
}

/// <summary>
/// MVC facade for the dropdown panel: <c>&lt;pems-dropdown-menu end="true" css="p-0"&gt;&lt;li&gt;...&lt;/pems-dropdown-menu&gt;</c> writes <c>&lt;ul class="dropdown-menu dropdown-menu-end p-0"&gt;</c>
/// (<c>as="div"</c> for a panel of free markup). Every other attribute and the content stay as the page wrote them.
/// </summary>
[HtmlTargetElement("pems-dropdown-menu")]
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
