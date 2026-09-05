namespace PEMS.Application.DTOs;

public class RecurringNcrPatternDto
{
    public int EquipmentId { get; set; }
    public string EquipmentTagNumber { get; set; } = "";
    public string EquipmentName { get; set; } = "";
    public int NcrCount { get; set; }
    public List<string> NcrNumbers { get; set; } = new();
    public string? DominantQCDiscipline { get; set; }
    public bool HasOpenMOC { get; set; }
    public string Suggestion { get; set; } = "";
}

public class DelayRootCauseAnalysisDto
{
    public int TotalDelayedDocuments { get; set; }
    public double AverageDelayDays { get; set; }
    public List<(string GroupName, int Count, double AvgDelayDays)> ByDiscipline { get; set; } = new();
    public List<(string GroupName, int Count, double AvgDelayDays)> ByVendor { get; set; } = new();
    public List<(string GroupName, int Count, double AvgDelayDays)> ByStage { get; set; } = new();

    // خلاصه‌ی روایی تولیدشده توسط Ollama از روی همین ارقام واقعی (نه فرضی) - اگر Ollama در
    // دسترس نباشد null می‌ماند و UI فقط جدول‌های عددی را نشان می‌دهد
    public string? NarrativeSummary { get; set; }
}
