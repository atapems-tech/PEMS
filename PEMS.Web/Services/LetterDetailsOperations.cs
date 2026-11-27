using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-828: the letter page as read operations over the services of LetterController.Details (see <see cref="ILetterDetailsOperations"/>).</summary>
public sealed class LetterDetailsOperations(ILetterService letters, ILetterComposeService compose, ITagService tags, INotificationService notifications, IPermissionService permissions, ICurrentUserService currentUser, PEMSDbContext context) : ILetterDetailsOperations
{
    private async Task<bool> ReviewerAsync() => await permissions.HasPermissionAsync(currentUser.Roles, "Correspondence", "Approve");

    private async Task<LetterViewer> ViewerAsync()
    {
        var userId = currentUser.UserId;
        int? employeeId = userId.HasValue ? await context.Users.Where(u => u.Id == userId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync() : null;
        return new LetterViewer(userId, employeeId, currentUser.Roles.Contains("SystemAdmin") || await ReviewerAsync(), currentUser.UserName);
    }

    private async Task<LetterDetailDto?> DetailAsync(int id)
    {
        var access = await letters.GetDetailForViewerAsync(id, await ViewerAsync());
        return access.Outcome == LetterAccessOutcome.Ok ? access.Detail : null;
    }

    public async Task<List<LetterHeadRow>> HeadAsync(int id)
    {
        var d = await DetailAsync(id);
        if (d == null) return [];
        var l = d.Letter;
        if (currentUser.UserId.HasValue) await notifications.MarkRelatedReadAsync(currentUser.UserId.Value, "Letter", id);
        var review = await ReviewerAsync();
        var isAdmin = currentUser.Roles.Contains("SystemAdmin");
        var isAuthor = currentUser.UserId.HasValue && currentUser.UserId.Value == l.RegisteredById;
        var currentApprover = l.Status == "PendingApproval" && d.ApprovalSteps.OrderBy(s => s.StepOrder).FirstOrDefault(s => s.Status == "Pending")?.ApproverUserId == currentUser.UserId;
        var archiveStatus = l.Status is "Sent" or "Closed" or "Issued" or "Acknowledged" or "Registered";
        return [new LetterHeadRow(l.LetterId, l.LetterNumber, l.Subject, l.LetterType, l.Status, l.Priority, l.ConfidentialityLevel, l.ProjectName, l.DisciplineName, l.Sender, l.Receiver, l.LetterDate, l.ReferenceNumber, l.Category,
            l.ResponseRequired, l.ResponseDueDate, l.ResponseAnswered, l.ResponseOverdue, l.SignedByName, l.SignedDate, d.Body ?? "", l.Tags.Count == 0 ? null : string.Join(", ", l.Tags.Select(t => t.Name)),
            l.Status == "Approved" && l.LetterType == "Internal" && isAuthor, (isAuthor || isAdmin || review) && archiveStatus, l.Status == "Draft" && isAuthor, l.Status == "PendingApproval" && (review || currentApprover),
            l.Status == "Approved" && review && l.LetterType != "Internal", l.Status == "Issued" && review, l.Status != "Draft" && l.Status != "PendingApproval" && l.Status != "Rejected", string.IsNullOrEmpty(l.SignedByName),
            (isAuthor || isAdmin || review) && !LetterResponseRules.FinishedStatuses.Contains(l.Status), isAuthor || isAdmin || review)];
    }

    public async Task<List<LetterRecipientRow>> RecipientsAsync(int id)
        => await DetailAsync(id) is { } d ? d.Recipients.Select(r => new LetterRecipientRow(r.LetterRecipientId, r.RecipientType, r.DisplayName, r.IsRead ? (r.ReadDate?.ToString("yyyy-MM-dd") ?? "Read") : "")).ToList() : [];

    public async Task<List<LetterReferralRow>> ReferralsAsync(int id)
        => await DetailAsync(id) is { } d ? d.Referrals.Select(r => new LetterReferralRow(r.LetterReferralId, r.ReferredToEmployeeName ?? r.ReferredToDepartmentName ?? "", r.Instructions, r.DueDate, r.Status, r.ReferredByName, r.ReferredDate, r.ResponseNote, r.IsOverdue, r.Status != "Completed")).ToList() : [];

    public async Task<List<LetterAttachmentRow>> AttachmentsAsync(int id)
        => await DetailAsync(id) is { } d ? d.Attachments.Select(a => new LetterAttachmentRow(a.AttachmentId, a.FileName)).ToList() : [];

    public async Task<List<LetterStepRow>> StepsAsync(int id)
    {
        var d = await DetailAsync(id);
        if (d == null) return [];
        var admin = currentUser.Roles.Contains("SystemAdmin") && d.Letter.Status == "PendingApproval";
        return d.ApprovalSteps.OrderBy(s => s.StepOrder).Select(s => new LetterStepRow(s.StepOrder, s.StepOrder, s.ApproverName, s.Status, s.DecidedDate, s.Comment, s.IsCurrent, admin && s.Status == "Pending")).ToList();
    }

    public async Task<List<LetterReviewRow>> ReviewsAsync(int id)
        => await DetailAsync(id) is { } d ? d.Reviews.Select(r => new LetterReviewRow(r.LetterReviewId, r.ReviewerName, r.Decision, r.Comment, r.CreatedDate)).ToList() : [];

    public async Task<List<LetterTimelineRow>> TimelineAsync(int id)
    {
        if (await DetailAsync(id) == null) return [];
        var n = 0;
        return (await letters.GetTimelineAsync(id, await ViewerAsync())).OrderBy(e => e.EventDate).Select(e => new LetterTimelineRow(++n, e.EventDate, e.EventType, e.Description, e.ActorName)).ToList();
    }

    public async Task<List<LetterTagRow>> TagsAsync(int id) => await DetailAsync(id) is { } d ? d.Letter.Tags.Select(t => new LetterTagRow(t.TagId, t.Name)).ToList() : [];

    public async Task<List<LetterOption>> TagOptionsAsync() => (await tags.GetAllAsync(currentUser.UserId)).Select(t => new LetterOption(t.TagId.ToString(), t.Name)).ToList();

    public async Task<List<LetterOption>> DepartmentsAsync() => await context.Departments.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new LetterOption(x.DepartmentId.ToString(), x.Name)).ToListAsync();

    public async Task<List<LetterOption>> ApproversAsync() => currentUser.Roles.Contains("SystemAdmin") ? (await compose.GetApproverOptionsAsync()).Select(a => new LetterOption(a.UserId.ToString(), a.FullName)).ToList() : [];
}
