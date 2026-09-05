using ClosedXML.Excel;

namespace PEMS.Web.Helpers;

/// <summary>
/// استایل هدر و تنظیمات پایانی شیت، عیناً بین Export های Users/Department/Employee تکرار
/// شده بود (رنگ پس‌زمینه، فونت سفید بولد، AdjustToContents، FreezeRows).
/// </summary>
public static class ExcelExportHelper
{
    private static readonly XLColor HeaderBackground = XLColor.FromHtml("#1F4E78");

    public static void AddStyledHeaderRow(this IXLWorksheet sheet, params string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = HeaderBackground;
            cell.Style.Font.FontColor = XLColor.White;
        }
    }

    public static void FinalizeExportSheet(this IXLWorksheet sheet)
    {
        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);
    }
}
