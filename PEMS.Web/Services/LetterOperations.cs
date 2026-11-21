using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-716, 715: the letter commands of LetterController and LetterComposeController (same access rules, same services, same texts). A letter the user may not see is a refusal (the 403 page, as before).</summary>
public sealed class LetterOperations(PEMSDbContext context, ILetterService letters, ILetterReferralService referrals, ITagService tags, ILetterComposeService compose, ICurrentUserService currentUser, IPermissionService permissions) : ILetterOperations
{
    private bool IsAdmin => currentUser.Roles.Contains("SystemAdmin");

    private async Task<bool> CanSeeAllAsync() => IsAdmin || await permissions.HasPermissionAsync(currentUser.Roles, "Correspondence", "Approve");

    private async Task<int?> EmployeeIdAsync()
        => currentUser.UserId.HasValue ? await context.Users.Where(u => u.Id == currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync() : null;

    // every action on a letter first asks whether the user may see it (a guessed id of a confidential letter of someone else is refused)
    private async Task AccessAsync(int letterId)
    {
        var viewer = new LetterViewer(currentUser.UserId, await EmployeeIdAsync(), await CanSeeAllAsync(), currentUser.UserName);
        if (!await letters.CanViewAsync(letterId, viewer)) throw new UnauthorizedAccessException();
    }

    private async Task ReviewerAsync(int letterId)
    {
        if (await CanSeeAllAsync()) return;
        var userId = currentUser.UserId;
        if (!userId.HasValue) throw new UnauthorizedAccessException();
        var current = await context.LetterApprovalSteps.AsNoTracking().Where(s => s.LetterId == letterId && s.Status == "Pending").OrderBy(s => s.StepOrder).Select(s => (int?)s.ApproverUserId).FirstOrDefaultAsync();
        if (current != userId.Value) throw new UnauthorizedAccessException();
    }

    private static byte[]? Version(string? rowVersion) => string.IsNullOrWhiteSpace(rowVersion) ? null : Convert.FromBase64String(rowVersion);

    private int UserIdOrDeny() => currentUser.UserId ?? throw new UnauthorizedAccessException();

    private static (bool Success, string? Error) Ok() => (true, null);

    public async Task<(bool Success, string? Error)> SetResponseDueAsync(int letterId, bool responseRequired, DateTime? responseDueDate)
    {
        await AccessAsync(letterId);
        return await letters.SetResponseDueAsync(letterId, responseRequired, responseDueDate, currentUser.UserId, await CanSeeAllAsync());
    }

    public async Task<(bool Success, string? Error)> ToggleStarAsync(int letterId)
    {
        await AccessAsync(letterId);
        if (currentUser.UserId.HasValue) await letters.ToggleStarAsync(letterId, currentUser.UserId.Value);
        return Ok();
    }

    public async Task<(bool Success, string? Error)> AddRecipientAsync(int letterId, string recipientType, int? employeeId, int? departmentId, string? externalName)
    {
        await AccessAsync(letterId);
        await letters.AddRecipientAsync(letterId, recipientType, employeeId, departmentId, externalName);
        return Ok();
    }

    public async Task<(bool Success, string? Error)> MarkRecipientReadAsync(int letterId, int letterRecipientId)
    {
        await AccessAsync(letterId);
        if (currentUser.UserId.HasValue) await letters.MarkRecipientReadAsync(letterRecipientId, currentUser.UserId.Value);
        return Ok();
    }

    public async Task<(bool Success, string? Error)> AttachTagAsync(int letterId, int? tagId, string? newTagName, string? newTagColor)
    {
        await AccessAsync(letterId);
        var idToAttach = tagId;
        if (!idToAttach.HasValue && !string.IsNullOrWhiteSpace(newTagName)) idToAttach = await tags.CreateAsync(newTagName, newTagColor ?? "secondary", currentUser.UserId);
        if (idToAttach.HasValue) await tags.AttachToLetterAsync(letterId, idToAttach.Value, currentUser.UserId);
        return Ok();
    }

    public async Task<(bool Success, string? Error)> DetachTagAsync(int letterId, int tagId)
    {
        await AccessAsync(letterId);
        await tags.DetachFromLetterAsync(letterId, tagId);
        return Ok();
    }

    public Task<(bool Success, string? Error)> SubmitForReviewAsync(int letterId) => letters.SubmitForReviewAsync(letterId, currentUser.UserId);

    public async Task<(bool Success, string? Error)> ApproveAsync(int letterId, string? comment) { await ReviewerAsync(letterId); return await letters.ApproveAsync(letterId, currentUser.UserId, comment); }

    public async Task<(bool Success, string? Error)> ReturnForRevisionAsync(int letterId, string reason) { await ReviewerAsync(letterId); return await letters.ReturnForRevisionAsync(letterId, currentUser.UserId, reason); }

    public async Task<(bool Success, string? Error)> RejectAsync(int letterId, string reason) { await ReviewerAsync(letterId); return await letters.RejectAsync(letterId, currentUser.UserId, reason); }

    public Task<(bool Success, string? Error)> IssueAsync(int letterId) => letters.IssueAsync(letterId, currentUser.UserId);

    public Task<(bool Success, string? Error)> CloseAsync(int letterId) => letters.CloseAsync(letterId, currentUser.UserId);

    public Task<(bool Success, string? Error)> SaveDraftAsync(int letterId, string subject, string body, string? rowVersion) => letters.SaveDraftEditAsync(letterId, subject, body, UserIdOrDeny(), Version(rowVersion));

    public Task<(bool Success, string? Error)> ResubmitAsync(int letterId, string subject, string body, string? rowVersion) => letters.ResubmitAsync(letterId, subject, body, UserIdOrDeny(), Version(rowVersion));

    public async Task<(bool Success, string? Error)> ReferAsync(int letterId, int? referredToEmployeeId, int? referredToDepartmentId, string? instructions, DateTime? dueDate)
    {
        await AccessAsync(letterId);
        await referrals.CreateAsync(letterId, referredToEmployeeId, referredToDepartmentId, instructions, dueDate, currentUser.FullName);
        return Ok();
    }

    public async Task<(bool Success, string? Error)> MarkReferralReadAsync(int referralId, int letterId)
    {
        await AccessAsync(letterId);
        if (currentUser.UserId != null) await referrals.MarkReadAsync(referralId, currentUser.UserId.Value);
        return Ok();
    }

    public async Task<(bool Success, string? Error)> CompleteReferralAsync(int referralId, int letterId, string? responseNote)
    {
        await AccessAsync(letterId);
        await referrals.CompleteAsync(referralId, responseNote);
        return Ok();
    }

    public async Task<(bool Success, string? Error)> SignAsync(int letterId, string signedByName)
    {
        await AccessAsync(letterId);
        await letters.SignAsync(letterId, signedByName);
        return Ok();
    }

    public Task<(bool Success, string? Error)> SendAsync(int letterId) => letters.SendAsync(letterId, currentUser.UserId);

    public async Task<(bool Success, string? Error)> ArchiveAsync(int letterId) => await letters.ArchiveAsync(letterId, currentUser.UserId, await CanSeeAllAsync());

    public Task<(bool Success, string? Error)> ReassignApproverAsync(int letterId, int stepOrder, int newApproverUserId) => letters.ReassignApprovalStepAsync(letterId, stepOrder, newApproverUserId, currentUser.UserId, IsAdmin);

    public async Task<(bool Success, string? Error)> DiscardDraftAsync(int letterId)
    {
        var userId = UserIdOrDeny();
        var manual = await permissions.HasPermissionAsync(currentUser.Roles, "Correspondence", "ManualNumber");
        return await compose.DiscardDraftAsync(letterId, new LetterComposeActor(userId, currentUser.UserName, await EmployeeIdAsync(), manual, IsAdmin));
    }
}
