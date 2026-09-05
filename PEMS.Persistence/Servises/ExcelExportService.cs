using ClosedXML.Excel;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Persistence.Services;

public class ExcelExportService : IExcelExportService
{
    public byte[] ExportDocumentRegister(List<EngineeringDocumentDto> documents)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Document Register");

        var headers = new[]
        {
            "Document No", "Title", "Type", "Discipline", "Project",
            "Revision", "Status", "Issue Purpose", "Created Date"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
            cell.Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        foreach (var d in documents)
        {
            sheet.Cell(row, 1).Value = d.DocumentNo;
            sheet.Cell(row, 2).Value = d.Title;
            sheet.Cell(row, 3).Value = d.DocumentType;
            sheet.Cell(row, 4).Value = d.DisciplineName;
            sheet.Cell(row, 5).Value = d.ProjectName;
            sheet.Cell(row, 6).Value = d.RevisionNo;
            sheet.Cell(row, 7).Value = d.Status;
            sheet.Cell(row, 8).Value = d.IssuePurpose;
            sheet.Cell(row, 9).Value = d.CreatedDate;
            sheet.Cell(row, 9).Style.DateFormat.Format = "yyyy-mm-dd";
            row++;
        }

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
