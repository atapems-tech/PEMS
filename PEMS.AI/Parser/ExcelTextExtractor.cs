using System.Text;
using ClosedXML.Excel;

namespace PEMS.Application.AI.Parser;

public class ExcelTextExtractor : ITextExtractor
{
    public bool CanHandle(string fileExtension) =>
        string.Equals(fileExtension, ".xlsx", StringComparison.OrdinalIgnoreCase);

    public Task<ExtractedDocumentText> ExtractAsync(string filePath)
    {
        var sb = new StringBuilder();
        int sheetCount = 0;

        using (var workbook = new XLWorkbook(filePath))
        {
            foreach (var worksheet in workbook.Worksheets)
            {
                sheetCount++;
                sb.AppendLine($"--- Sheet: {worksheet.Name} ---");

                foreach (var row in worksheet.RowsUsed())
                {
                    var cellValues = row.CellsUsed()
                        .Select(c => c.GetFormattedString().Trim())
                        .Where(v => !string.IsNullOrEmpty(v));

                    var line = string.Join(" | ", cellValues);
                    if (!string.IsNullOrWhiteSpace(line))
                        sb.AppendLine(line);
                }
            }
        }

        var result = new ExtractedDocumentText { FullText = sb.ToString(), UsedOcr = false, PageCount = sheetCount };
        return Task.FromResult(result);
    }
}
