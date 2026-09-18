namespace PEMS.Web.Models;

/// <summary>
/// طبق «PEMS Help System — Phase 3»: ساختار درختیِ ساده‌شده برای رندر Decision Tree/Entity
/// Relationship - ساخته‌شده از HelpVisualRenderDto (Node/Edge تخت) در کد C# معمولی (نه در
/// خودِ Razor)، تا Partial رندرکننده فقط یک درخت آماده را با Recursion استاندارد Razor
/// (فراخوانی خودش برای هر فرزند) نمایش دهد - بدون ساخت دستیِ رشته‌ی HTML.
/// </summary>
public class HelpNodeTreeItem
{
    public string Label { get; set; } = "";
    public string IconCssClass { get; set; } = "bi-circle";
    public string? IncomingEdgeLabel { get; set; }
    public bool IsRoot { get; set; }
    public List<HelpNodeTreeItem> Children { get; set; } = new();
}
