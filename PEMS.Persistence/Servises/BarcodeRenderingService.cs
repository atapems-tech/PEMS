using PEMS.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ZXing;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS — Enterprise Barcode System» بند «Rendering / Label Engine»: تولید کاملا محلی
/// (On-Premises) - رمزگذاری واقعی نماد (Code128/QR) با ZXing.Net (بدون هیچ وابستگی بیرونی/
/// SaaS)، خروجی SVG/PNG/PDF با همون QuestPDF که از قبل در پروژه (LetterController) استفاده
/// می‌شود - بدون افزودن یک کتابخانه‌ی Rendering جدید.
/// </summary>
public class BarcodeRenderingService : IBarcodeRenderingService
{
    // طبق مجوز Community همون QuestPDF که از قبل در Program.cs برای LetterController تنظیم
    // می‌شود - این‌جا هم صریحاً تنظیم می‌شود چون این سرویس ممکن است خارج از خط لوله‌ی وب‌اپ
    // (مثلاً تست‌های واحد) هم فراخوانی شود، جایی که Program.cs هرگز اجرا نمی‌شود.
    static BarcodeRenderingService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static BarcodeFormat ResolveFormat(string symbology) => symbology switch
    {
        "QR" => BarcodeFormat.QR_CODE,
        "Code39" => BarcodeFormat.CODE_39,
        _ => BarcodeFormat.CODE_128
    };

    public string RenderSvg(string code, string symbology, int moduleSizePx = 3)
    {
        var writer = new BarcodeWriterSvg
        {
            Format = ResolveFormat(symbology),
            Options = new ZXing.Common.EncodingOptions
            {
                Margin = 2,
                Width = symbology == "QR" ? 240 : 360,
                Height = symbology == "QR" ? 240 : 90
            }
        };

        return writer.Write(code).Content;
    }

    public byte[] RenderPng(string code, string symbology, int widthPx = 400)
    {
        var svg = RenderSvg(code, symbology);
        var heightPx = symbology == "QR" ? widthPx : widthPx / 4;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(widthPx, heightPx, Unit.Point);
                page.Margin(0);
                // FitArea: سایز واقعی SVG (پیکسلی از ZXing) اهمیتی ندارد - همیشه داخل صفحه جا می‌شود
                page.Content().Svg(svg).FitArea();
            });
        });

        var images = document.GenerateImages(new ImageGenerationSettings
        {
            ImageFormat = ImageFormat.Png,
            RasterDpi = 300
        });

        return images.First();
    }
}
