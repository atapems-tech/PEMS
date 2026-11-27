using ClosedXML.Excel;
using PEMS.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PEMS.Web.Helpers;

/// <summary>
/// خروجی چاپی «Part Catalogue Application» (فرم INWH-PLA-FO-001 Rev.3). Excel دقیقاً روی همان قالب رسمی («نمونه فرم اکسل.xlsx»،
/// EmbeddedResource) پر می‌شود: ظاهر/Merge/عرض ستون‌ها دست‌نخورده و فقط سلول‌های داده نوشته می‌شوند. قالب سه ستون (D/E/F)
/// برای سه قطعه دارد؛ هر درخواست فقط ستون D را می‌گیرد و نمونه‌ی ستون‌های E/F پاک می‌شود. PDF با QuestPDF همان چیدمان را می‌سازد.
/// </summary>
public static class PartCataloguePrintExport
{
    private const string TemplateResource = "PEMS.Templates.INWH-PLA-FO-001.xlsx";

    private static string Or(string? s, string fallback = "") => string.IsNullOrWhiteSpace(s) ? fallback : s.Trim();

    public static byte[] BuildXlsx(PartCataloguePrintModel m)
    {
        using var tpl = typeof(PartCataloguePrintExport).Assembly.GetManifestResourceStream(TemplateResource)
                        ?? throw new InvalidOperationException("Print template resource not found: " + TemplateResource);
        using var wb = new XLWorkbook(tpl);
        var ws = wb.Worksheets.First();

        // نمونه‌ی قالب (قطعه‌ی دوم و سوم) پاک می‌شود - فقط مقدار، نه Style/Merge
        for (var row = 10; row <= 35; row++)
            foreach (var col in new[] { "D", "E", "F" })
                ws.Cell($"{col}{row}").Clear(XLClearOptions.Contents);

        ws.Cell("B7").Value = (m.IsChange ? "☐ " : "☒ ") + "ADD";
        ws.Cell("C7").Value = (m.IsChange ? "☒ " : "☐ ") + "CHANGE";
        ws.Cell("F8").Value = m.RequestNumber;

        ws.Cell("D10").Value = m.PartNumber;
        ws.Cell("D11").Value = m.TechnicalClassName;
        ws.Cell("D12").Value = Or(m.Material, "N/A");
        ws.Cell("D13").Value = Or(m.Attributes, "N/A");
        ws.Cell("D14").Value = Or(m.EquipmentSerial, "N/A");
        ws.Cell("D15").Value = Or(m.SparePartSerial, "N/A");
        ws.Cell("D16").Value = Or(m.EquipmentDocNo, "N/A");
        ws.Cell("D17").Value = Or(m.ManufacturerName, "N/A");
        ws.Cell("D18").Value = Or(m.ManufacturerPartNo, "N/A");
        ws.Cell("D19").Value = Or(m.ManufacturerDwgNo, "N/A");
        ws.Cell("D20").Value = Or(m.SupplierName, "N/A");
        ws.Cell("D21").Value = Or(m.SupplierPartNo, "N/A");
        ws.Cell("D22").Value = Or(m.RelatedEquipmentTags, "N/A");
        ws.Cell("D23").Value = m.Unit;
        ws.Cell("D24").Value = m.CommodityGroup1;
        ws.Cell("D25").Value = m.ProductFamily;
        ws.Cell("D26").Value = m.MinStock;
        ws.Cell("D27").Value = m.MaxConsumptionRate;
        ws.Cell("D28").Value = m.InstalledQuantity;

        if (m.WantedReceiptDate.HasValue)
        {
            ws.Cell("D29").Value = "Determined Date: " + m.WantedReceiptDate.Value.ToString("yyyy-MM-dd");
            ws.Cell("D32").Value = "Planed  ✔";
        }
        else ws.Cell("D30").Value = "UnPlaned  ✔";

        ws.Cell("D33").Value = m.StorageVolume;
        ws.Cell("D34").Value = m.StockGrade;
        ws.Cell("D35").Value = m.IsCritical ? "YES" : "NO";

        PrintSignature Sig(string role) => m.Signatures.FirstOrDefault(s => s.Role == role) ?? new PrintSignature(role, "", null, "");
        var o = Sig("Originator"); var sp = Sig("SpareSpecialist"); var mp = Sig("MaintPlanning"); var pc = Sig("PartCataloguer"); var ic = Sig("InventoryController");
        string D(PrintSignature s) => s.Date?.ToString("yyyy-MM-dd") ?? "";

        ws.Cell("B37").Value =
            $"Originator Name: {o.Name}      Spare Specialist Name: {sp.Name}      Maint. Planning Name: {mp.Name}      " +
            $"Part Cataloguer Name: {pc.Name}      Inventory Controller Name: {ic.Name}";
        ws.Cell("B39").Value = $"Date: {D(o)}      Date: {D(sp)}      Date: {D(mp)}      Date: {D(pc)}      Date: {D(ic)}";
        ws.Cell("B40").Value = $"Tel No: {o.Tel}      Tel No: {sp.Tel}      Tel No: {mp.Tel}      Tel No: {pc.Tel}      Tel No: {ic.Tel}";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();

    }
}
