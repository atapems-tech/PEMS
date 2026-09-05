namespace PEMS.Application.DTOs;

public class CorrespondenceReportDto
{
    public int TotalLetters { get; set; }
    public int TotalIncoming { get; set; }
    public int TotalOutgoing { get; set; }
    public int TotalInternal { get; set; }
    public int OverdueReferrals { get; set; }
    public double? AvgReferralCompletionDays { get; set; }

    // "2026-01" -> count, به‌ترتیب ۱۲ ماه اخیر
    public List<(string Month, int Incoming, int Outgoing)> VolumeByMonth { get; set; } = new();

    public List<(string Discipline, int Count)> ByDiscipline { get; set; } = new();

    public List<(string Status, int Count)> ByStatus { get; set; } = new();

    public List<(string TagName, string Color, int Count)> TopTags { get; set; } = new();

    public List<(string Sender, int Count)> TopSenders { get; set; } = new();
}
