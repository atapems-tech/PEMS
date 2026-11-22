using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-817: the reads of the tender details page (formerly TenderController.Details). An unknown tender gives no rows; nothing here changes data.</summary>
public sealed class TenderDetailsOperations(PEMSDbContext context, ITenderInvitationService invitations, IPermissionService permissions, ICurrentUserService currentUser) : ITenderDetailsOperations
{
    private static string Text(string? s) => s ?? string.Empty;
    private static string Id(int v) => v.ToString(CultureInfo.InvariantCulture);

    public async Task<List<TenderHeadRow>> HeadAsync(int id)
    {
        var t = await context.Tenders.AsNoTracking().Include(x => x.ProcurementPackage).Include(x => x.AwardedBid).ThenInclude(b => b!.Vendor).FirstOrDefaultAsync(x => x.TenderId == id);
        if (t is null) return [];
        var mayEdit = await permissions.HasPermissionAsync(currentUser.Roles, "Tender", "Edit");
        var awarded = t.AwardedBid is null ? "" : $"{t.AwardedBid.Vendor?.Name} — {t.AwardedBid.FinalValue?.ToString("N0", CultureInfo.InvariantCulture)} {t.AwardedBid.Currency}".Trim();
        return [new TenderHeadRow(t.TenderId, t.TenderId, t.TenderNumber, t.Title, t.ProcurementPackage?.PackageNumber ?? "", t.ProcurementPackageId, t.Status, t.ResponseDeadline, t.RequiresVendorQualification, awarded, t.ContractId,
            t.Status == "Draft", t.Status == "Open", t.Status == "Awarded" && t.ContractId == null, mayEdit && invitations.EmailEnabled && t.Status is "Draft" or "Open")];
    }

    public async Task<List<TenderVendorRow>> VendorsAsync(int id) =>
        (await context.TenderInvitedVendors.AsNoTracking().Include(x => x.Vendor).Where(x => x.TenderId == id && x.IsActive).ToListAsync())
            .Select(v => new TenderVendorRow(v.VendorId, id, Text(v.Vendor?.Name), v.QualificationConfirmed)).ToList();

    public async Task<List<TenderInvitationRow>> InvitationLogAsync(int id)
    {
        var mayEdit = await permissions.HasPermissionAsync(currentUser.Roles, "Tender", "Edit");
        var status = await context.Tenders.AsNoTracking().Where(t => t.TenderId == id).Select(t => t.Status).FirstOrDefaultAsync();
        var canSend = mayEdit && invitations.EmailEnabled && status is "Draft" or "Open";
        return (await invitations.GetLogAsync(id)).Select(d => new TenderInvitationRow(d.VendorId, d.VendorName, Text(d.ToAddress), d.Status, d.Attempts, Text(d.QueuedBy), d.QueuedAt, d.SentAt, Text(d.LastError),
            canSend && d.Status is "Failed" or "Skipped")).ToList();
    }

    public async Task<List<TenderBidRow>> BidsAsync(int id)
    {
        var t = await context.Tenders.AsNoTracking().Include(x => x.Bids).ThenInclude(b => b.Vendor).Include(x => x.Bids).ThenInclude(b => b.Evaluations).Include(x => x.Bids).ThenInclude(b => b.Negotiations)
            .FirstOrDefaultAsync(x => x.TenderId == id);
        if (t is null) return [];
        return t.Bids.Select(b =>
        {
            var technical = b.Evaluations.Any(e => e.EvaluationType == "Technical" && e.Decision == "Passed");
            var commercial = b.Evaluations.Any(e => e.EvaluationType == "Commercial" && e.Decision == "Passed");
            var pending = b.Negotiations.FirstOrDefault(n => n.Status == "Proposed");
            return new TenderBidRow(b.BidId, id, Text(b.Vendor?.Name), b.BidNumber, b.Status, b.OriginalValue, b.NegotiatedValue, Text(b.Currency), technical ? "Passed" : "Pending", commercial ? "Passed" : "Pending",
                pending?.BidNegotiationId, t.Status is "Open" or "Closed", t.Status == "Closed", t.Status == "Closed" && pending != null, t.Status == "Closed" && technical && commercial);
        }).ToList();
    }

    public async Task<List<TenderClarificationRow>> ClarificationsAsync(int id) =>
        (await context.ProcurementCommunications.AsNoTracking().Include(x => x.Vendor).Include(x => x.Responses).Where(x => x.TenderId == id).OrderByDescending(x => x.RaisedDate).ToListAsync())
            .Select(c => new TenderClarificationRow(c.ProcurementCommunicationId, id, c.ProcurementPackageId, c.Type, c.Subject, Text(c.Vendor?.Name), c.Body, c.ActionStatus,
                string.Join(Environment.NewLine, c.Responses.OrderBy(r => r.ResponseDate).Select(r => $"{r.ResponseDate:yyyy-MM-dd} {r.RespondedByName}: {r.ResponseText}")), c.ActionStatus != "Closed")).ToList();

    public async Task<List<ReportChoiceRow>> InvitedVendorChoicesAsync(int? tenderId = null)
    {
        if (tenderId is null) return [];
        return (await context.TenderInvitedVendors.AsNoTracking().Include(x => x.Vendor).Where(x => x.TenderId == tenderId && x.IsActive).ToListAsync())
            .Select(v => new ReportChoiceRow(Id(v.VendorId), Text(v.Vendor?.Name))).ToList();
    }

    public async Task<List<ReportChoiceRow>> VendorChoicesAsync() =>
        (await context.Vendors.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new { x.VendorId, x.Name }).ToListAsync()).Select(v => new ReportChoiceRow(Id(v.VendorId), v.Name)).ToList();

    public async Task<List<TenderPendingNegotiationRow>> PendingNegotiationAsync(int bidId)
    {
        var bid = await context.Bids.AsNoTracking().Include(b => b.Vendor).Include(b => b.Negotiations).FirstOrDefaultAsync(b => b.BidId == bidId);
        var pending = bid?.Negotiations.FirstOrDefault(n => n.Status == "Proposed");
        return pending is null ? [] : [new TenderPendingNegotiationRow(bidId, bidId, pending.BidNegotiationId, Text(bid!.Vendor?.Name), pending.NegotiatedValue)];
    }
}
