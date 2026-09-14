namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS — Enterprise Barcode System» بند «Rendering / Label Engine»: تولید محلی
/// (On-Premises، بدون هیچ سرویس/SaaS بیرونی) - SVG (بردار، برای چاپ صنعتی)، PNG (پیش‌نمایش
/// وب)، و PDF برچسب (تکی/دسته‌ای). موتور رمزنگاری واقعی نمادها (Code128/QR) از ZXing.Net است؛
/// PDF/PNG با همون QuestPDF موجود در پروژه تولید می‌شود (بدون افزودن کتابخانه‌ی رندر جدید).
/// </summary>
public interface IBarcodeRenderingService
{
    string RenderSvg(string code, string symbology, int moduleSizePx = 3);

    byte[] RenderPng(string code, string symbology, int widthPx = 400);

    /// <summary>
    /// طبق بند «Label Generation»/«Bulk»: یک یا چند برچسب صنعتی (هر برچسب: بارکد + شناسه‌ی
    /// خوانا برای انسان + Metadata کوتاه) را در یک PDF واحد می‌چیند.
    /// </summary>
    byte[] RenderLabelSheetPdf(List<BarcodeLabelContent> labels);
}

/// <summary>
/// طبق بند «Do not hard-code layout»: محتوای هر برچسب از بیرون (Controller/Service که رکورد
/// هدف واقعی را می‌شناسد) تامین می‌شود - موتور Rendering چیزی درباره‌ی Equipment/Material
/// نمی‌داند، فقط متن/بارکد را می‌چیند.
/// </summary>
public class BarcodeLabelContent
{
    public string Code { get; set; } = "";
    public string Symbology { get; set; } = "Code128";
    public string TitleLine { get; set; } = "";
    public string? SubtitleLine { get; set; }
    public string? MetaLine { get; set; }
}
