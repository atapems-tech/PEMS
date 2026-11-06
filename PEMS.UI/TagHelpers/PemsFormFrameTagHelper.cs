using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.Components;
using PEMS.UI.Rendering;

namespace PEMS.UI.TagHelpers;

/// <summary>
/// MVC facade for the form element: <c>&lt;pems-form method="post" asp-action="Save"&gt;fields&lt;/pems-form&gt;</c> becomes a <c>form</c> with the same action,
/// antiforgery field and attributes as the built-in helper writes (this class derives from it), written by the registered <c>form-frame</c> component.
/// </summary>
[HtmlTargetElement("pems-form")]
[PemsFacade(UiKeys.FormFrame)]
public sealed class PemsFormFrameTagHelper(IHtmlGenerator generator, IPemsComponentRenderer renderer) : FormTagHelper(generator)
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        base.Process(context, output);
        // A form with no action posts to the page's own address. Written out here, because the Blazor renderer would otherwise add an action of its
        // own (and a second one next to an empty action).
        var action = output.Attributes.TryGetAttribute("action", out var a) ? a.Value?.ToString() : null;
        if (string.IsNullOrEmpty(action))
        {
            output.Attributes.RemoveAll("action");
            var request = ViewContext?.HttpContext?.Request;
            if (request is not null)
                output.Attributes.Add(new TagHelperAttribute("action", request.PathBase.Add(request.Path).ToUriComponent() + request.QueryString.ToUriComponent()));
        }
        var (css, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsFormFrame>(renderer, output, new FormFrameOptions { PageClass = css, Attributes = attrs }, await ListRender.InnerAfterFramework(output));
    }
}
