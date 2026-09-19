using ClosedXML.Excel;
using PEMS.Application.DTOs;

namespace PEMS.Web.Helpers;

/// <summary>
/// ساخت فایل Excel فهرست مدارک مهندسی (همان ستون‌های فهرست Index) با ExcelExportHelper مشترک پروژه -
/// بدون منطق کسب‌وکاری؛ فقط نمایش. ردیف‌ها قبلاً با فیلتر و محدودیت دیسیپلین کاربر انتخاب شده‌اند.
/// </summary>
public static class EngineeringDocumentListExport
{
    public static readonly string[] Headers =
        { "Document No", "Title", "Type", "Discipline", "Area", "Project", "Revision", "Status", "Comment Status", "Frozen", "Delay (days)", "Created" };

    public static byte[] Build(IReadOnlyList<EngineeringDocumentDto> items)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Engineering Documents");
        sheet.AddStyledHeaderRow(Headers);

        var row = 2;
        foreach (var d in items)
        {
            sheet.Cell(row, 1).Value = d.DocumentNo;
            sheet.Cell(row, 2).Value = d.Title;
            sheet.Cell(row, 3).Value = d.DocumentType;
            sheet.Cell(row, 4).Value = d.DisciplineName;
            sheet.Cell(row, 5).Value = d.Area;
            sheet.Cell(row, 6).Value = d.ProjectName;
            sheet.Cell(row, 7).Value = d.RevisionNo;
            sheet.Cell(row, 8).Value = d.Status;
            sheet.Cell(row, 9).Value = d.CommentStatus;
            sheet.Cell(row, 10).Value = d.IsFrozen ? "Yes" : "";
            sheet.Cell(row, 11).Value = d.DelayDays;
            sheet.Cell(row, 12).Value = d.CreatedDate.ToString("yyyy-MM-dd");
            row++;
        }

        sheet.FinalizeExportSheet();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
