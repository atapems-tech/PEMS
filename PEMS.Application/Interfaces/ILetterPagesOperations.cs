namespace PEMS.Application.Interfaces;

public sealed record LetterListRow(int Id, string Number, string Subject, string Type, string? SenderReceiver, string Status, string Priority, bool Starred, bool Unread, DateTime? ResponseDue, bool ResponseOverdue, DateTime Date, string? Discipline, int Attachments, DateTime LastActivity, string? Snippet);
public sealed record LetterCountsRow(int Id, int Inbox, int InboxUnread, int Shared, int Sent, int Drafts, int Starred, int Archive, int Trash, int All, int Delegated);
public sealed record LetterDashboardRow(int Id, int TotalActive, int OpenLetters, int Received30, int Issued30, int OpenReferrals, int OverdueReferrals, int OverdueResponses, int AwaitingApproval, int MyDrafts, int MyReturned, int MyOpenReferrals);
public sealed record LetterBreakdownRow(string Key, int Count);
public sealed record LetterAttentionRow(int Id, string Number, string Subject, string Reason, DateTime Due, int DaysOverdue);
public sealed record LetterReportRow(int Id, int Received, int Unread, int Sent, int OpenReferrals, int OverdueReferrals, int CompletedReferrals, string AvgReferralDays, int ResponseRequired, string AvgResponseDays, int OnTimePercent, int AnsweredOnTime, int AnsweredLate, int OpenOverdue, int OpenWithinSla);
public sealed record LetterBucketRow(string Label, int Count);
public sealed record LetterSubordinateRow(int Id, string Name, string? Position, int Received, int Unread, int OpenReferrals, int OverdueReferrals, int CompletedReferrals, string AvgReferralDays);
public sealed record LetterFileResult(bool Success, string? Error, byte[] Bytes, string FileName, string ContentType);
public sealed record LetterChoice(string Value, string Label);

/// <summary>SCRUM-828: the list, counters, export, dashboard and my referrals of LetterController as operations over the same services, with the confidentiality rule of the controller (Correspondence.Approve or SystemAdmin sees confidential letters).</summary>
public interface ILetterPagesOperations
{
    [PEMS.Application.Common.PemsOperation("LetterPage", "List", "Correspondence.View")]
    Task<List<LetterListRow>> ListAsync(string? letterType = null, string? letterNumber = null, string? subject = null, string? sender = null, string? receiver = null, string? projectId = null, string? disciplineId = null, string? status = null,
        string? fromDate = null, string? toDate = null, string? starred = null, string? priority = null, string? hasAttachments = null, string? hasReferrals = null, string? overdueReferral = null, string? overdueResponse = null, string? unread = null,
        string? q = null, string? folder = null, string? tagId = null, string? confidentiality = null);

    [PEMS.Application.Common.PemsOperation("LetterPage", "Counts", "Correspondence.View")]
    Task<List<LetterCountsRow>> CountsAsync();

    [PEMS.Application.Common.PemsOperation("LetterPage", "Export", "Correspondence.View")]
    Task<LetterFileResult> ExportAsync(string? format = null, string? letterType = null, string? letterNumber = null, string? subject = null, string? sender = null, string? receiver = null, string? projectId = null, string? disciplineId = null, string? status = null,
        string? fromDate = null, string? toDate = null, string? starred = null, string? priority = null, string? hasAttachments = null, string? hasReferrals = null, string? overdueReferral = null, string? overdueResponse = null, string? unread = null,
        string? q = null, string? folder = null, string? tagId = null, string? confidentiality = null);

    [PEMS.Application.Common.PemsOperation("LetterPage", "Dashboard", "Correspondence.View")]
    Task<List<LetterDashboardRow>> DashboardAsync();

    [PEMS.Application.Common.PemsOperation("LetterPage", "DashboardStatus", "Correspondence.View")]
    Task<List<LetterBreakdownRow>> DashboardStatusAsync();

    [PEMS.Application.Common.PemsOperation("LetterPage", "DashboardType", "Correspondence.View")]
    Task<List<LetterBreakdownRow>> DashboardTypeAsync();

    [PEMS.Application.Common.PemsOperation("LetterPage", "DashboardAging", "Correspondence.View")]
    Task<List<LetterBreakdownRow>> DashboardAgingAsync();

    [PEMS.Application.Common.PemsOperation("LetterPage", "Attention", "Correspondence.View")]
    Task<List<LetterAttentionRow>> AttentionAsync();

    [PEMS.Application.Common.PemsOperation("LetterPage", "MyReferrals", "Correspondence.View")]
    Task<List<LetterListRow>> MyReferralsAsync();

    [PEMS.Application.Common.PemsOperation("LetterPage", "Projects", "Correspondence.View")]
    Task<List<LetterChoice>> ProjectsAsync();

    [PEMS.Application.Common.PemsOperation("LetterPage", "Disciplines", "Correspondence.View")]
    Task<List<LetterChoice>> DisciplinesAsync();

    // ---- cartable report (SCRUM-828)
    [PEMS.Application.Common.PemsOperation("LetterPage", "Report", "Correspondence.View")]
    Task<List<LetterReportRow>> ReportAsync(string? from = null, string? to = null);

    [PEMS.Application.Common.PemsOperation("LetterPage", "ReportMonths", "Correspondence.View")]
    Task<List<LetterBucketRow>> ReportMonthsAsync(string? from = null, string? to = null);

    [PEMS.Application.Common.PemsOperation("LetterPage", "ReportDisciplines", "Correspondence.View")]
    Task<List<LetterBucketRow>> ReportDisciplinesAsync(string? from = null, string? to = null);

    [PEMS.Application.Common.PemsOperation("LetterPage", "ReportSla", "Correspondence.View")]
    Task<List<LetterBucketRow>> ReportSlaAsync(string? from = null, string? to = null);

    [PEMS.Application.Common.PemsOperation("LetterPage", "ReportSubordinates", "Correspondence.View")]
    Task<List<LetterSubordinateRow>> ReportSubordinatesAsync(string? from = null, string? to = null);

}
