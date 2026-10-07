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

    public static byte[] BuildPdf(PartCataloguePrintModel m)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var rows = new List<(string Label, string Value)>
        {
            ("MESD Part Number", m.PartNumber),
            ("Name (Technical Class)", m.TechnicalClassName),
            ("Material", Or(m.Material, "N/A")),
            ("Description / Other Information (Attributes)", Or(m.Attributes, "N/A")),
            ("Serial No. (Equipment)", Or(m.EquipmentSerial, "N/A")),
            ("Serial No. (Spare Part)", Or(m.SparePartSerial, "N/A")),
            ("Doc. No. (Equipment tag No)", Or(m.EquipmentDocNo, "N/A")),
            ("Manufacturer - Name", Or(m.ManufacturerName, "N/A")),
            ("Manufacturer - Part No.", Or(m.ManufacturerPartNo, "N/A")),
            ("Manufacturer - Dwg. No.", Or(m.ManufacturerDwgNo, "N/A")),
            ("Supplier - Name", Or(m.SupplierName, "N/A")),
            ("Supplier - Part No.", Or(m.SupplierPartNo, "N/A")),
            ("Related Equipment TAG No (Object ID)", Or(m.RelatedEquipmentTags, "N/A")),
            ("Unit", m.Unit),
            ("Commodity Group 1", m.CommodityGroup1),
            ("Product Family", m.ProductFamily),
            ("Min Stock", m.MinStock),
            ("Max Consumption Rate (one year)", m.MaxConsumptionRate),
            ("Installed quantity on equipment", m.InstalledQuantity),
            ("Wanted Receipt Date", m.WantedReceiptDate?.ToString("yyyy-MM-dd") ?? "UnPlaned"),
            ("Storage Volume", m.StorageVolume),
            ("Stock Grade", m.StockGrade),
            ("Critical", m.IsCritical ? "YES" : "NO"),
        };

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Border(1).Padding(4).Row(r =>
                    {
                        r.RelativeItem().Text("Warehouse Department").Bold().FontSize(11);
                        r.RelativeItem().AlignCenter().Text("Part Catalogue Application").Bold().FontSize(12);
                        r.RelativeItem().AlignRight().Text(t =>
                        {
                            t.Line("Doc. No. INWH-PLA-FO-001");
                            t.Line("Rev.3");
                            if (!string.IsNullOrEmpty(m.RequestNumber)) t.Line("Request: " + m.RequestNumber);
                        });
                    });
                    col.Item().PaddingTop(3).Text("* Please attach all supporting documentation and Drawings to this application").FontSize(8).Italic();
                    col.Item().PaddingVertical(3).Row(r =>
                    {
                        r.AutoItem().Text((m.IsChange ? "☐ " : "☒ ") + "ADD").Bold();
                        r.AutoItem().PaddingLeft(20).Text((m.IsChange ? "☒ " : "☐ ") + "CHANGE").Bold();
                    });
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.ConstantColumn(170); c.RelativeColumn(); });
                    foreach (var (label, value) in rows)
                    {
                        table.Cell().Border(0.5f).Background(Colors.Grey.Lighten4).Padding(3).Text(label).SemiBold();
                        table.Cell().Border(0.5f).Padding(3).Text(string.IsNullOrWhiteSpace(value) ? " " : value);
                    }
                });

                page.Footer().Column(col =>
                {
                    col.Item().Text("Approval Process").Bold();
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { for (var i = 0; i < 5; i++) c.RelativeColumn(); });
                        var heads = new[] { "Originator", "Spare Specialist", "Maint. Planning", "Part Cataloguer", "Inventory Controller" };
                        var roles = new[] { "Originator", "SpareSpecialist", "MaintPlanning", "PartCataloguer", "InventoryController" };
                        foreach (var h in heads) t.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(2).Text(h).SemiBold().FontSize(8);
                        foreach (var r in roles)
                        {
                            var s = m.Signatures.FirstOrDefault(x => x.Role == r);
                            t.Cell().Border(0.5f).Padding(3).MinHeight(46).Column(c =>
                            {
                                c.Item().Text("Name: " + (s?.Name ?? "")).FontSize(8);
                                c.Item().Text("Date: " + (s?.Date?.ToString("yyyy-MM-dd") ?? "")).FontSize(8);
                                c.Item().Text("Tel No: " + (s?.Tel ?? "")).FontSize(8);
                            });
                        }
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }
}
