using System.Net;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.Components;
using PEMS.UI.Rendering;

namespace PEMS.UI.TagHelpers;

/// <summary>Shared by the overlay frames: the id and the other attributes of the page's tag, as the component wants them.</summary>
internal static class OverlayTagHelper
{
    public static (string Id, IReadOnlyDictionary<string, object>? Others) Split(TagHelperOutput output)
    {
        var id = "";
        var rest = new Dictionary<string, object>();
        foreach (var a in output.Attributes)
        {
            var value = a.Value switch
            {
                null => "",
                IHtmlContent html => WebUtility.HtmlDecode(ToHtml(html)),
                _ => a.Value.ToString() ?? ""
            };
            if (a.Name.Equals("id", StringComparison.OrdinalIgnoreCase)) id = value;
            else rest[a.Name] = value;
        }
        output.Attributes.Clear();
        return (id, rest.Count == 0 ? null : rest);
    }

    /// <summary>Every attribute of the page's tag (id included), values decoded for the component.</summary>
    public static IReadOnlyDictionary<string, object>? All(TagHelperOutput output)
    {
        if (output.Attributes.Count == 0) return null;
        var all = new Dictionary<string, object>();
        foreach (var a in output.Attributes)
            all[a.Name] = a.Value switch { null => "", IHtmlContent html => WebUtility.HtmlDecode(ToHtml(html)), _ => a.Value.ToString() ?? "" };
        return all;
    }

    private static string ToHtml(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }
}

/// <summary>
/// MVC facade for the modal frame: <c>&lt;pems-modal id="x" size="large" tabindex="-1"&gt;&lt;form&gt;header, body, footer&lt;/form&gt;&lt;/pems-modal&gt;</c> writes
/// <c>modal fade</c>, the dialog (<c>modal-dialog modal-lg</c>) and <c>modal-content</c> around the page's own markup; id, tabindex, aria-* and data-* stay on the modal.
/// </summary>
[HtmlTargetElement("pems-modal")]
public sealed class PemsModalTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    /// <summary>small, medium (default), large or extralarge.</summary>
    [HtmlAttributeName("size")] public string Size { get; set; } = "medium";
    /// <summary>Extra classes on the dialog (modal-dialog-scrollable, modal-dialog-centered).</summary>
    [HtmlAttributeName("dialog-css")] public string? DialogCss { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var inner = (await output.GetChildContentAsync()).GetContent();
        var (id, rest) = OverlayTagHelper.Split(output);
        BlockTagHelper.Replace(output, await renderer.RenderAsync<PemsModal>(new Dictionary<string, object?>
        {
            [nameof(PemsModal.Options)] = new ModalOptions
            {
                Id = id, Title = "", CloseLabel = "", Frame = true, Size = Enum.Parse<OverlaySize>(Size, ignoreCase: true),
                DialogCssClass = DialogCss, CssClass = Css, Attributes = rest
            },
            [nameof(PemsModal.ChildContent)] = BlockTagHelper.Markup(inner)
        }));
    }
}

/// <summary>MVC facade for the side panel frame: <c>&lt;pems-offcanvas id="menu" side="start" tabindex="-1"&gt;header and body markup&lt;/pems-offcanvas&gt;</c>.</summary>
[HtmlTargetElement("pems-offcanvas")]
public sealed class PemsOffcanvasTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    /// <summary>end (default) or start.</summary>
    [HtmlAttributeName("side")] public string Side { get; set; } = "end";
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var inner = (await output.GetChildContentAsync()).GetContent();
        var (id, rest) = OverlayTagHelper.Split(output);
        BlockTagHelper.Replace(output, await renderer.RenderAsync<PemsDrawer>(new Dictionary<string, object?>
        {
            [nameof(PemsDrawer.Options)] = new DrawerOptions
            {
                Id = id, Title = "", CloseLabel = "", Frame = true, Side = Enum.Parse<DrawerSide>(Side, ignoreCase: true), CssClass = Css, Attributes = rest
            },
            [nameof(PemsDrawer.ChildContent)] = BlockTagHelper.Markup(inner)
        }));
    }
}
