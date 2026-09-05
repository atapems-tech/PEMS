namespace PEMS.Application.AI.Models;

public class AIAnalysisResult
{
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }

    public string Summary { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public List<string> ExtractedTags { get; set; } = new();
    public string DocumentTypePrediction { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public bool UsedOcr { get; set; }

    // هشدارهایی که مدل درباره‌ی چانک ناقص یا جدول بریده‌شده گزارش می‌دهد - باید در UI بازبینی نمایش داده شود
    public List<string> Warnings { get; set; } = new();

    public ExtractedEngineeringData? ExtractedData { get; set; }

    // فقط وقتی از AnalyzeTableDocumentAsync برمی‌گردد پر می‌شود؛ برای مسیر عمومی AnalyzeDocumentAsync همیشه null است
    public ExtractedTableData? ExtractedTableData { get; set; }
}
