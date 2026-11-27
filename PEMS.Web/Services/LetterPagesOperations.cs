using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Helpers;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-828: the letter list, counters, export, dashboard and my referrals as operations (see <see cref="ILetterPagesOperations"/>).</summary>
public sealed class LetterPagesOperations(ILetterService letters, ILetterReferralService referrals, ILetterCartableService cartable, IPermissionService permissions, ICurrentUserService currentUser, PEMSDbContext context) : ILetterPagesOperations
{
    private static readonly string[] KnownPriorities = ["Low", "Normal", "High", "Urgent"];
    private static int? Int(string? s) => int.TryParse(s, out var v) ? v : null;
    private static DateTime? Date(string? s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    private static bool Flag(string? s) => string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
    private static bool? Tri(string? s) => string.IsNullOrWhiteSpace(s) ? null : Flag(s);
    private static string? Text(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task<bool> ConfidentialAsync() => currentUser.Roles.Contains("SystemAdmin") || await permissions.HasPermissionAsync(currentUser.Roles, "Correspondence", "Approve");

    private static LetterAdvancedFilter Advanced(string? priority, string? hasAttachments, string? hasReferrals, string? overdueReferral, string? overdueResponse, string? unread, string? q, string? folder, string? tagId, string? confidentiality)
        => new()
        {
            Priority = KnownPriorities.FirstOrDefault(p => string.Equals(p, priority?.Trim(), StringComparison.OrdinalIgnoreCase)), HasAttachments = Tri(hasAttachments), HasReferrals = Tri(hasReferrals), OverdueReferralOnly = Flag(overdueReferral),
            OverdueResponseOnly = Flag(overdueResponse), UnreadOnly = Flag(unread), Query = Text(q), Folder = LetterFolders.Normalize(folder), TagId = Int(tagId), Confidentiality = Text(confidentiality), SortDescending = true
        };

    private static LetterListRow Row(LetterDto l) => new(l.LetterId, l.LetterNumber, l.Subject, l.LetterType, string.Equals(l.LetterType, "Outgoing", StringComparison.OrdinalIgnoreCase) ? l.Receiver : l.Sender, l.Status, l.Priority, l.IsStarred, l.IsUnread,
        l.ResponseDueDate ?? l.NextResponseDue, l.ResponseOverdue, l.LetterDate, l.DisciplineName, l.AttachmentsCount, l.LastActivityDate, l.Snippet);

    public async Task<List<LetterListRow>> ListAsync(string? letterType = null, string? letterNumber = null, string? subject = null, string? sender = null, string? receiver = null, string? projectId = null, string? disciplineId = null, string? status = null,
        string? fromDate = null, string? toDate = null, string? starred = null, string? priority = null, string? hasAttachments = null, string? hasReferrals = null, string? overdueReferral = null, string? overdueResponse = null, string? unread = null,
        string? q = null, string? folder = null, string? tagId = null, string? confidentiality = null)
    {
        var result = await letters.SearchAsync(Text(letterType), Text(letterNumber), Text(subject), Text(sender), Text(receiver), Int(projectId), Int(disciplineId), Text(status), Date(fromDate), Date(toDate), 1, 2000, await ConfidentialAsync(), currentUser.UserId, Flag(starred),
            Advanced(priority, hasAttachments, hasReferrals, overdueReferral, overdueResponse, unread, q, folder, tagId, confidentiality));
        return result.Items.Select(Row).ToList();
    }

    public async Task<List<LetterCountsRow>> CountsAsync()
    {
        var c = currentUser.UserId.HasValue ? await cartable.GetCountsAsync(currentUser.UserId.Value, await ConfidentialAsync()) : new CartableCountsDto();
        return [new LetterCountsRow(1, c.Inbox, c.InboxUnread, c.Shared, c.Sent, c.Drafts, c.Starred, c.Archive, c.Trash, c.All, c.Delegated)];
    }

    public async Task<LetterFileResult> ExportAsync(string? format = null, string? letterType = null, string? letterNumber = null, string? subject = null, string? sender = null, string? receiver = null, string? projectId = null, string? disciplineId = null, string? status = null,
        string? fromDate = null, string? toDate = null, string? starred = null, string? priority = null, string? hasAttachments = null, string? hasReferrals = null, string? overdueReferral = null, string? overdueResponse = null, string? unread = null,
        string? q = null, string? folder = null, string? tagId = null, string? confidentiality = null)
    {
        var items = await letters.ExportAsync(Text(letterType), Text(letterNumber), Text(subject), Text(sender), Text(receiver), Int(projectId), Int(disciplineId), Text(status), Date(fromDate), Date(toDate), await ConfidentialAsync(), currentUser.UserId, Flag(starred),
            Advanced(priority, hasAttachments, hasReferrals, overdueReferral, overdueResponse, unread, q, folder, tagId, confidentiality));
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        return string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)
            ? new(true, null, LetterListExport.BuildCsv(items), $"Correspondence_{stamp}.csv", "text/csv; charset=utf-8")
            : new(true, null, LetterListExport.BuildExcel(items), $"Correspondence_{stamp}.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private async Task<LetterWorkspaceDashboardDto> WorkspaceAsync() { var review = await ConfidentialAsync(); return await letters.GetWorkspaceDashboardAsync(review, currentUser.UserId, review); }

    public async Task<List<LetterDashboardRow>> DashboardAsync()
    {
        var d = await WorkspaceAsync();
        return [new LetterDashboardRow(1, d.TotalActive, d.OpenLetters, d.ReceivedLast30Days, d.IssuedLast30Days, d.OpenReferrals, d.OverdueReferrals, d.OverdueResponses, d.AwaitingApproval, d.MyDrafts, d.MyReturnedForRevision, d.MyOpenReferrals)];
    }

    public async Task<List<LetterBreakdownRow>> DashboardStatusAsync() => (await WorkspaceAsync()).ByStatus.Select(x => new LetterBreakdownRow(x.Key, x.Value)).ToList();
    public async Task<List<LetterBreakdownRow>> DashboardTypeAsync() => (await WorkspaceAsync()).ByType.Select(x => new LetterBreakdownRow(x.Key, x.Value)).ToList();
    public async Task<List<LetterBreakdownRow>> DashboardAgingAsync() => (await WorkspaceAsync()).Aging.Select(x => new LetterBreakdownRow(x.Key, x.Count)).ToList();
    public async Task<List<LetterAttentionRow>> AttentionAsync() => (await WorkspaceAsync()).NeedsAttention.Select(a => new LetterAttentionRow(a.LetterId, a.LetterNumber, a.Subject, a.Reason, a.DueDate, a.DaysOverdue)).ToList();

    public async Task<List<LetterListRow>> MyReferralsAsync()
        => currentUser.UserId == null ? [] : (await referrals.GetMyReferralsAsync(currentUser.UserId.Value)).Select(Row).ToList();

    public async Task<List<LetterChoice>> ProjectsAsync() => await context.Projects.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.ProjectName).Select(x => new LetterChoice(x.ProjectId.ToString(), x.ProjectName)).ToListAsync();
    public async Task<List<LetterChoice>> DisciplinesAsync() => await context.Disciplines.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new LetterChoice(x.DisciplineId.ToString(), x.Name)).ToListAsync();
}
