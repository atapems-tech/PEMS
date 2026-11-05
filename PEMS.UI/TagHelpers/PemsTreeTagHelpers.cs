using PEMS.UI.Rendering;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PEMS.UI.TagHelpers;

// SCRUM-337 TreeView: a hierarchy (folders, positions, menu items) as a WAI-ARIA tree: role="tree" / "treeitem" / "group", a
// toggle per branch, keyboard (arrows, Home, End, Enter) and the collapsed branches remembered per tree (pems-tree.js, loaded by
// the layout). A branch that holds the current item is never collapsed. Styles: pems-ui.css (pems-tree*), RTL from the same markup.
// Usage: docs/design/LIST_PAGE_COMPONENT.md (TreeView).

/// <summary>What a tree passes to its items (shared through TagHelperContext.Items).</summary>
public sealed class PemsTreeContext
{
    public string ToggleLabel { get; init; } = "";
}

/// <summary>What an item's children tell it: it has children; its label markup.</summary>
public sealed class PemsTreeItemContext
{
    public bool HasChildren { get; set; }
    public string? LabelHtml { get; set; }
}

/// <summary>
/// The tree: <c>label</c> is its accessible name, <c>toggle-label</c> the text of the expand / collapse buttons ("Expand or
/// collapse: Engineering"), <c>remember-key</c> the browser storage key of the collapsed branches (none: not remembered).
/// Child content: <c>pems-tree-item</c>s. A class or id on the tag is kept.
/// </summary>
[HtmlTargetElement("pems-tree")]
[PemsFacade(FacadeKind.Direct)]
public sealed class PemsTreeTagHelper : TagHelper
{
    [HtmlAttributeName("label")] public string Label { get; set; } = "";
    [HtmlAttributeName("toggle-label")] public string ToggleLabel { get; set; } = "";
    [HtmlAttributeName("remember-key")] public string? RememberKey { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        context.Items[typeof(PemsTreeContext)] = new PemsTreeContext { ToggleLabel = ToggleLabel };
        PemsList.Become(output, "ul", "pems-tree");
        output.Attributes.SetAttribute("role", "tree");
        if (!string.IsNullOrEmpty(Label)) output.Attributes.SetAttribute("aria-label", Label);
        output.Attributes.SetAttribute("data-pems-tree", "");
        if (!string.IsNullOrEmpty(RememberKey)) output.Attributes.SetAttribute("data-pems-tree-remember", RememberKey);
        output.Content.SetHtmlContent(await output.GetChildContentAsync());
    }
}

/// <summary>
/// One node: <c>label</c> (also the toggle's name), <c>key</c> (remembers its collapsed state), <c>href</c> (a link) or plain
/// text, <c>current</c> (the page it opens; never collapsed), <c>count</c> (a number after the label), <c>expanded</c> (default
/// open), <c>link-class</c> (the page's own class on the link or text). A <c>pems-tree-label</c> child replaces the label text
/// with the page's own markup; nested <c>pems-tree-item</c>s are its children.
/// </summary>
[HtmlTargetElement("pems-tree-item")]
[PemsFacade(FacadeKind.Direct)]
public sealed class PemsTreeItemTagHelper : TagHelper
{
    [HtmlAttributeName("label")] public string Label { get; set; } = "";
    [HtmlAttributeName("key")] public string? Key { get; set; }
    [HtmlAttributeName("href")] public string? Href { get; set; }
    [HtmlAttributeName("current")] public bool Current { get; set; }
    [HtmlAttributeName("count")] public int? Count { get; set; }
    [HtmlAttributeName("expanded")] public bool Expanded { get; set; } = true;
    [HtmlAttributeName("link-class")] public string? LinkClass { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // tell the parent item it has a child, then give the children their own context
        if (context.Items.TryGetValue(typeof(PemsTreeItemContext), out var parentObj) && parentObj is PemsTreeItemContext parent) parent.HasChildren = true;
        var tree = context.Items.TryGetValue(typeof(PemsTreeContext), out var t) ? t as PemsTreeContext : null;
        var mine = new PemsTreeItemContext();
        context.Items[typeof(PemsTreeItemContext)] = mine;
        var children = await output.GetChildContentAsync();

        output.TagName = "li";
        output.TagMode = TagMode.StartTagAndEndTag;
        var own = output.Attributes.TryGetAttribute("class", out var c) ? " " + c.Value : "";
        output.Attributes.SetAttribute("class", "pems-tree__item" + own);
        output.Attributes.SetAttribute("role", "treeitem");
        if (!string.IsNullOrEmpty(Key)) output.Attributes.SetAttribute("data-key", Key);
        if (Current) output.Attributes.SetAttribute("data-current", "");
        if (mine.HasChildren) output.Attributes.SetAttribute("aria-expanded", Expanded ? "true" : "false");

        var row = new TagBuilder("div");
        row.AddCssClass("pems-tree__row");
        if (mine.HasChildren)
        {
            var toggle = new TagBuilder("button");
            toggle.Attributes["type"] = "button";
            toggle.AddCssClass("pems-tree__toggle");
            toggle.Attributes["aria-expanded"] = Expanded ? "true" : "false";
            var name = string.IsNullOrEmpty(tree?.ToggleLabel) ? Label : tree!.ToggleLabel + ": " + Label;
            toggle.Attributes["aria-label"] = name;
            toggle.Attributes["title"] = name;
            toggle.InnerHtml.AppendHtml(PemsList.Icon("bi-chevron-right"));
            row.InnerHtml.AppendHtml(toggle);
        }
        else
        {
            var spacer = new TagBuilder("span");
            spacer.AddCssClass("pems-tree__spacer");
            spacer.Attributes["aria-hidden"] = "true";
            row.InnerHtml.AppendHtml(spacer);
        }

        var link = new TagBuilder(string.IsNullOrEmpty(Href) ? "span" : "a");
        link.AddCssClass(string.IsNullOrEmpty(Href) ? "pems-tree__text" : "pems-tree__link");
        if (!string.IsNullOrEmpty(LinkClass)) link.AddCssClass(LinkClass);
        if (!string.IsNullOrEmpty(Href)) link.Attributes["href"] = Href;
        if (Current) link.Attributes["aria-current"] = "page";
        if (mine.LabelHtml != null) link.InnerHtml.AppendHtml(mine.LabelHtml);
        else link.InnerHtml.Append(Label);
        if (Count.HasValue)
        {
            var count = new TagBuilder("span");
            count.AddCssClass("pems-tree__count");
            count.InnerHtml.Append(Count.Value.ToString(System.Globalization.CultureInfo.CurrentCulture));
            link.InnerHtml.AppendHtml(count);
        }
        row.InnerHtml.AppendHtml(link);

        output.Content.SetHtmlContent(row);
        if (mine.HasChildren)
        {
            var group = new TagBuilder("ul");
            group.AddCssClass("pems-tree__group");
            group.Attributes["role"] = "group";
            if (!Expanded) group.Attributes["hidden"] = "hidden";
            group.InnerHtml.AppendHtml(children);
            output.Content.AppendHtml(group);
        }
    }
}

/// <summary>The page's own markup for the label of the enclosing <c>pems-tree-item</c> (a colour dot, an icon, a badge).</summary>
[HtmlTargetElement("pems-tree-label", ParentTag = "pems-tree-item")]
[PemsFacade(FacadeKind.Direct)]
public sealed class PemsTreeLabelTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var html = (await output.GetChildContentAsync()).GetContent();
        if (context.Items.TryGetValue(typeof(PemsTreeItemContext), out var o) && o is PemsTreeItemContext item) item.LabelHtml = html;
        output.SuppressOutput();
    }
}
