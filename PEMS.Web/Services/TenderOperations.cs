using System.Globalization;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-759: the list, invitations and clarifications of TenderController (it used the context inline), as operations.</summary>
public sealed class TenderOperations(PEMSDbContext context, IProcurementPackageService packages, ITenderInvitationService invitations, IBackgroundJobClient jobs, IStringLocalizer<SharedResource> localizer,
    IConfiguration configuration, ICurrentUserService currentUser, ITenderService tenders, IPermissionService permissions, IAccessControlService accessControl) : ITenderOperations
{
    public async Task<List<TenderRow>> TendersAsync(int? procurementPackageId = null)
    {
        var query = context.Tenders.AsNoTracking().Include(x => x.ProcurementPackage).Include(x => x.Bids).AsQueryable();
        if (procurementPackageId.HasValue) query = query.Where(x => x.ProcurementPackageId == procurementPackageId.Value);
        return (await query.OrderByDescending(x => x.TenderId).Take(500).ToListAsync()).Select(t => new TenderRow(t.TenderId, t.TenderNumber, t.Title, t.ProcurementPackage?.PackageNumber ?? "", t.ProcurementPackageId, t.Status, t.IssueDate, t.ResponseDeadline, t.Bids.Count, t.ContractId)).ToList();
    }

    public async Task<(bool Success, string? Error)> SendInvitationsAsync(string? userName, int tenderId, int? vendorId = null)
    {
        var template = new TenderInvitationTemplate(localizer["Tinv_MailSubject"].Value, localizer["Tinv_MailBody"].Value, CultureInfo.CurrentUICulture, configuration["Letterhead:CompanyName"]);
        var result = await invitations.QueueAsync(tenderId, template, userName, vendorId.HasValue ? [vendorId.Value] : null);
        if (result.Error != null) return (false, localizer[result.Error].Value);
        foreach (var dispatchId in result.QueuedDispatchIds) jobs.Enqueue<ITenderInvitationService>(s => s.SendAsync(dispatchId));
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RaiseClarificationAsync(int tenderId, int procurementPackageId, string type, string subject, string body, int? vendorId = null)
    {
        await packages.AddCommunicationAsync(procurementPackageId, type, subject, body, vendorId, currentUser.FullName, null, tenderId);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RespondClarificationAsync(int procurementPackageId, int communicationId, string responseText)
    {
        await packages.RespondToCommunicationAsync(procurementPackageId, communicationId, responseText, currentUser.FullName);
        return (true, null);
    }

    // ---- SCRUM-817: the guarded commands of TenderController (separation of duties and creator separation), as operations. The permission Tender.Approve is checked by the engine; the shared
    // access decision (role, identity, scope, separation of duties, audit row) and the creator-approver separation run here, as in the controller.

    private async Task<int?> EmployeeIdAsync() =>
        currentUser.UserId.HasValue ? await context.Users.Where(u => u.Id == currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync() : null;

    /// <summary>Null = go on; else the refusal.</summary>
    private async Task<TenderStep?> GuardAsync(int tenderId, string? ip)
    {
        var outcome = await ProtectedActionGuard.EvaluateAsync(null, permissions, accessControl, currentUser, "Tender", "Approve", tenderId.ToString(), ip);
        if (outcome == ProtectedActionOutcome.DeniedBySoD) return new(false, "طبق سیاست تفکیک وظایف، این اقدام برای شما مجاز نیست.");
        return outcome == ProtectedActionOutcome.Allowed ? null : new(false, "You may not do this.");
    }

    private async Task<TenderStep?> SeparationAsync(int? tenderId, int? bidNegotiationId, string? ip)
    {
        var result = await tenders.CheckCreatorApproverSeparationAsync(tenderId, bidNegotiationId, currentUser.UserId, currentUser.UserName,
            currentUser.Roles.Contains(PEMS.Persistence.Identity.SystemRoles.SystemAdmin), ip);
        return result switch
        {
            TenderApprovalSeparation.DeniedCreator => new(false, "طبق سیاست تفکیک وظایف، ایجادکننده‌ی مناقصه نمی‌تواند همان مناقصه را تأیید کند."),
            TenderApprovalSeparation.DeniedUnverifiable => new(false, "You may not do this."),
            _ => null
        };
    }

    public async Task<TenderStep> ApproveNegotiationAsync(int tenderId, int bidNegotiationId, string? ip)
    {
        // the tender is taken from the negotiation itself, not from the form (P0-10)
        if (await SeparationAsync(null, bidNegotiationId, ip) is { } refused) return refused;
        var result = await tenders.ApproveNegotiationAsync(bidNegotiationId, await EmployeeIdAsync());
        return new(result.Success, result.Error);
    }

    public async Task<TenderStep> SelectWinnerAsync(int tenderId, int bidId, string? ip)
    {
        if (await GuardAsync(tenderId, ip) is { } blocked) return blocked;
        if (await SeparationAsync(tenderId, null, ip) is { } refused) return refused;
        var result = await tenders.SelectWinnerAsync(tenderId, bidId, await EmployeeIdAsync());
        return new(result.Success, result.Error);
    }

    public async Task<TenderStep> CreateContractAsync(int tenderId, string contractNo, DateTime? startDate, DateTime? endDate, string? ip)
    {
        // creating the contract is an approval of its own: Tender.Approve (guard) and Contracts.Create
        if (await GuardAsync(tenderId, ip) is { } blocked) return blocked;
        if (!await permissions.HasPermissionAsync(currentUser.Roles, "Contracts", "Create")) return new(false, "You may not do this.");
        if (await SeparationAsync(tenderId, null, ip) is { } refused) return refused;

        var result = await tenders.CreateContractFromWinningBidAsync(tenderId, contractNo, startDate, endDate, currentUser.UserId, ip);
        // the commit was uncertain and the re-read did not settle it: never say "not created"
        if (result.OutcomeUnknown) return new(false, result.Error);
        if (result.Success)
        {
            var problems = new List<string>();
            if (result.GenericAuditFailed) problems.Add("ثبت AuditLog عمومی این عملیات با خطا مواجه شد (در لاگ سیستم ثبت شده است)");
            if (result.CreatorEvent == ContractCreatorEventStatus.NotRecorded) problems.Add("رویداد «ایجادکننده‌ی قرارداد» ثبت نشد؛ برای ترمیم، همین عملیات را دوباره اجرا کنید (قرارداد تکراری ساخته نمی‌شود)");
            if (problems.Count > 0) return new(true, "قرارداد ساخته و ذخیره شد، اما " + string.Join("؛ ", problems) + ".", null, result.ContractId);
            if (result.RecoveredAfterUncertainCommit) return new(true, "Contract draft created from winning bid (the commit result was uncertain and was confirmed by re-reading the database; no duplicate was created)", null, result.ContractId);
            return new(true, null, null, result.ContractId);
        }
        if (result.Committed && result.ContractId.HasValue)
        {
            // the contract exists already; a retry creates no duplicate and only repairs the creator event
            return result.CreatorEvent switch
            {
                ContractCreatorEventStatus.Repaired => new(true, "قرارداد از قبل ساخته شده بود؛ رویداد ناقص «ایجادکننده‌ی قرارداد» برای ایجادکننده‌ی اصلی ترمیم شد. قرارداد تکراری ساخته نشد.", null, result.ContractId),
                ContractCreatorEventStatus.NotRecorded => new(false, "قرارداد از قبل ساخته شده است، اما ترمیم رویداد «ایجادکننده‌ی قرارداد» ناموفق بود؛ دوباره تلاش کنید."),
                ContractCreatorEventStatus.OutcomeUnknown => new(false, "قرارداد از قبل ساخته شده است، اما وضعیت رویداد «ایجادکننده‌ی قرارداد» قابل تأیید نبود؛ گزارش تشخیص را بررسی کنید."),
                ContractCreatorEventStatus.CreatorUnidentified => new(false, "قرارداد از قبل ساخته شده است، اما ایجادکننده‌ی اصلی آن قابل شناسایی نیست؛ رویداد نوشته نشد و نیاز به بررسی دستی دارد."),
                _ => new(false, result.Error)
            };
        }
        return new(false, result.Error);
    }
}
