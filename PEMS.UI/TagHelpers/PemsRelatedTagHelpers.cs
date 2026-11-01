using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PEMS.UI.TagHelpers;

// SCRUM-337 RelatedList: the child collection of a detail page (tasks of a job plan, lines of a receipt, rules of a table):
// a titled section with a count and an empty text, the child table, an add form under it, a delete form per row and, for
// tables edited in place, a hidden form per row whose inputs point at it with form="...". The page keeps its own columns,
// fields, actions and permissions. Styles: _content/PEMS.UI/pems-list.css (lp-rel-*). Usage: docs/design/LIST_PAGE_COMPONENT.md.

internal static class PemsRelated
{
    /// <summary>The antiforgery field of the current request, the same one the form tag helper adds (none outside a request).</summary>
    public static TagBuilder? Antiforgery(ViewContext? viewContext)
    {
        var http = viewContext?.HttpContext;
        if (http?.RequestServices?.GetService(typeof(IAntiforgery)) is not IAntiforgery antiforgery) return null;
        var tokens = antiforgery.GetAndStoreTokens(http);
        if (string.IsNullOrEmpty(tokens.FormFieldName) || string.IsNullOrEmpty(tokens.RequestToken)) return null;
        return Hidden(tokens.FormFieldName, tokens.RequestToken);
    }

    public static TagBuilder Hidden(string name, string? value)
    {
        var input = new TagBuilder("input");
        input.TagRenderMode = TagRenderMode.SelfClosing;
        input.Attributes["type"] = "hidden";
        input.Attributes["name"] = name;
        input.Attributes["value"] = value ?? "";
        return input;
    }

    /// <summary>A row action: icon-only submit button with its label as tool tip and for screen readers.</summary>
    public static TagBuilder ActButton(string icon, string label, string tone)
    {
        var b = new TagBuilder("button");
        b.Attributes["type"] = "submit";
        b.AddCssClass("lp-act lp-act--always " + tone);
        b.Attributes["title"] = label;
        b.InnerHtml.AppendHtml(PemsList.Icon(icon));
        b.InnerHtml.AppendHtml(PemsList.Hidden(label));
        return b;
    }

    public static TagBuilder PostForm(string action, ViewContext? viewContext)
    {
        var form = new TagBuilder("form");
        form.Attributes["method"] = "post";
        form.Attributes["action"] = action;
        var token = Antiforgery(viewContext);
        if (token != null) form.InnerHtml.AppendHtml(token);
        return form;
    }
}

/// <summary>
/// The section of one child collection: icon, title and count in the header, the page's table (child content) and, when
/// <c>empty</c>, the empty text. <c>id</c> lets other parts of the page link to it.
/// </summary>
[HtmlTargetElement("pems-related-list")]
public sealed class PemsRelatedListTagHelper : TagHelper
{
    [HtmlAttributeName("id")] public string? Id { get; set; }
    [HtmlAttributeName("icon")] public string? Icon { get; set; }
    [HtmlAttributeName("title")] public string Title { get; set; } = "";
    [HtmlAttributeName("count")] public int? Count { get; set; }
    [HtmlAttributeName("empty")] public bool Empty { get; set; }
    [HtmlAttributeName("empty-text")] public string? EmptyText { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        PemsList.Become(output, "section", "lp-rel");
        var headingId = (string.IsNullOrEmpty(Id) ? "rel" + context.UniqueId : Id) + "Title";
        if (!string.IsNullOrEmpty(Id)) output.Attributes.SetAttribute("id", Id);
        output.Attributes.SetAttribute("aria-labelledby", headingId);

        var head = new TagBuilder("div");
        head.AddCssClass("lp-rel-head");
        var h3 = new TagBuilder("h3");
        h3.Attributes["id"] = headingId;
        if (!string.IsNullOrEmpty(Icon)) { h3.InnerHtml.AppendHtml(PemsList.Icon(Icon)); h3.InnerHtml.Append(" "); }
        h3.InnerHtml.Append(Title);
        head.InnerHtml.AppendHtml(h3);
        if (Count.HasValue)
        {
            var count = new TagBuilder("span");
            count.AddCssClass("lp-total");
            count.InnerHtml.Append(Count.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            head.InnerHtml.AppendHtml(count);
        }

        output.PreContent.AppendHtml(head);
        output.Content.AppendHtml(await output.GetChildContentAsync());
        if (Empty && !string.IsNullOrEmpty(EmptyText))
        {
            var empty = new TagBuilder("p");
            empty.AddCssClass("lp-rel-empty");
            empty.InnerHtml.Append(EmptyText);
            output.Content.AppendHtml(empty);
        }
    }
}

/// <summary>The child table (thead and tbody are the page's child content), scrolling sideways on narrow screens.</summary>
[HtmlTargetElement("pems-related-table")]
public sealed class PemsRelatedTableTagHelper : TagHelper
{
    [HtmlAttributeName("id")] public string? Id { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var table = new TagBuilder("table");
        table.AddCssClass("table table-sm align-middle mb-0 lp-rel-table");
        if (!string.IsNullOrEmpty(Id)) table.Attributes["id"] = Id;
        // a page may put its own data-* hooks on the tag; they stay on the table
        foreach (var a in output.Attributes.Where(a => a.Name.StartsWith("data-", StringComparison.Ordinal)))
            table.Attributes[a.Name] = a.Value?.ToString() ?? "";
        table.InnerHtml.AppendHtml(await output.GetChildContentAsync());

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.Clear();
        output.Attributes.SetAttribute("class", "lp-rel-scroll");
        output.Content.SetHtmlContent(table);
    }
}

/// <summary>
/// The add form under the table: the parent key as a hidden field, the page's fields (child content) in one wrapping row and
/// a submit button (<c>submit-label</c>; icon only when <c>icon-only</c>).
/// </summary>
[HtmlTargetElement("pems-related-add")]
public sealed class PemsRelatedAddTagHelper : TagHelper
{
    [ViewContext, HtmlAttributeNotBound] public ViewContext? ViewContext { get; set; }
    [HtmlAttributeName("action")] public string Action { get; set; } = "";
    [HtmlAttributeName("parent-name")] public string? ParentName { get; set; }
    [HtmlAttributeName("parent-value")] public string? ParentValue { get; set; }
    [HtmlAttributeName("submit-label")] public string SubmitLabel { get; set; } = "";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var form = PemsRelated.PostForm(Action, ViewContext);
        form.AddCssClass("lp-rel-add");
        if (!string.IsNullOrEmpty(ParentName)) form.InnerHtml.AppendHtml(PemsRelated.Hidden(ParentName, ParentValue));
        form.InnerHtml.AppendHtml(await output.GetChildContentAsync());

        var submit = new TagBuilder("button");
        submit.Attributes["type"] = "submit";
        submit.AddCssClass("btn btn-sm btn-pems-solid lp-rel-submit");
        submit.InnerHtml.AppendHtml(PemsList.Icon("bi-plus-lg"));
        submit.InnerHtml.Append(" " + SubmitLabel);
        form.InnerHtml.AppendHtml(submit);

        output.TagName = null;
        output.Content.SetHtmlContent(form);
    }
}

/// <summary>
/// The delete action of one row: a small form posting the row key (and the parent key, to come back to the parent), with an
/// icon button whose label is its tool tip; <c>confirm</c> asks first.
/// </summary>
[HtmlTargetElement("pems-related-remove", TagStructure = TagStructure.WithoutEndTag)]
public sealed class PemsRelatedRemoveTagHelper : TagHelper
{
    [ViewContext, HtmlAttributeNotBound] public ViewContext? ViewContext { get; set; }
    [HtmlAttributeName("action")] public string Action { get; set; } = "";
    [HtmlAttributeName("key-name")] public string KeyName { get; set; } = "";
    [HtmlAttributeName("key-value")] public string? KeyValue { get; set; }
    [HtmlAttributeName("parent-name")] public string? ParentName { get; set; }
    [HtmlAttributeName("parent-value")] public string? ParentValue { get; set; }
    [HtmlAttributeName("label")] public string Label { get; set; } = "";
    [HtmlAttributeName("confirm")] public string? Confirm { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var form = PemsRelated.PostForm(Action, ViewContext);
        form.AddCssClass("lp-rel-inline");
        if (!string.IsNullOrEmpty(Confirm))
        {
            form.Attributes["data-confirm"] = Confirm;
            form.Attributes["onsubmit"] = "return confirm(this.dataset.confirm)";
        }
        form.InnerHtml.AppendHtml(PemsRelated.Hidden(KeyName, KeyValue));
        if (!string.IsNullOrEmpty(ParentName)) form.InnerHtml.AppendHtml(PemsRelated.Hidden(ParentName, ParentValue));
        form.InnerHtml.AppendHtml(PemsRelated.ActButton("bi-x-lg", Label, "text-danger"));

        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetHtmlContent(form);
    }
}

/// <summary>
/// In-place editing of one row: a hidden form (id = <c>id</c>) with the row's hidden keys (child content); the inputs in the
/// row's cells carry <c>form="id"</c>, and <c>&lt;pems-related-save form-id="id"&gt;</c> submits it.
/// </summary>
[HtmlTargetElement("pems-related-row-form")]
public sealed class PemsRelatedRowFormTagHelper : TagHelper
{
    [ViewContext, HtmlAttributeNotBound] public ViewContext? ViewContext { get; set; }
    [HtmlAttributeName("id")] public string Id { get; set; } = "";
    [HtmlAttributeName("action")] public string Action { get; set; } = "";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var form = PemsRelated.PostForm(Action, ViewContext);
        form.Attributes["id"] = Id;
        form.Attributes["hidden"] = "hidden";
        form.InnerHtml.AppendHtml(await output.GetChildContentAsync());
        output.TagName = null;
        output.Content.SetHtmlContent(form);
    }
}

/// <summary>The save button of a row edited in place (submits the row's form named by <c>form-id</c>).</summary>
[HtmlTargetElement("pems-related-save", TagStructure = TagStructure.WithoutEndTag)]
public sealed class PemsRelatedSaveTagHelper : TagHelper
{
    [HtmlAttributeName("form-id")] public string FormId { get; set; } = "";
    [HtmlAttributeName("label")] public string Label { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var button = PemsRelated.ActButton("bi-check2", Label, "text-primary");
        button.Attributes["form"] = FormId;
        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetHtmlContent(button);
    }
}
