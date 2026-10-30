using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Seed;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Persistence.Services;

/// <summary>
/// SCRUM-470: purchase-order lines from the pre-procurement path and approval by the PO authority table (owner decisions
/// 2026-10-29). Lines come from MR items (registered part kept); a stock line must be catalogued, a free-text line must be
/// non-stock with a commodity group and a reason (SAP text item / IFS no-part line). Submitting routes the PO to the role of the
/// matching authority rule through the PurchaseOrderApproval workflow (task in My Work), or leaves it "responsible not set".
/// The creator and the submitter can never approve (SoD). An approved PO is not a receipt, a stock entry or a payment.
/// </summary>
public class PurchaseOrderApprovalService : IPurchaseOrderApprovalService
{
    private const string Module = "PurchaseOrder";
    private const string MrIssuedToProcurement = "Issued to Procurement";
    private static readonly string[] EndedMrStatuses = { "Closed", "Cancelled" };
    private static readonly string[] DeadPoStatuses = { PurchaseOrderStatuses.Cancelled };
    private const string RunningInstance = "Running";

    private readonly PEMSDbContext _context;
    private readonly IWorkflowRuntime _runtime;
    private readonly IPermissionService _permissions;
    private readonly IPurchaseOrderService _orders;
    private readonly ProcurementOptions _options;

    public PurchaseOrderApprovalService(PEMSDbContext context, IWorkflowRuntime runtime, IPermissionService permissions,
        IPurchaseOrderService orders, IOptions<ProcurementOptions>? options = null)
    {
        _context = context;
        _runtime = runtime;
        _permissions = permissions;
        _orders = orders;
        _options = options?.Value ?? new ProcurementOptions();
    }

    // ================================================================== sources

    public async Task<IReadOnlyList<(int Id, string Number, string Description, int ProjectId)>> GetRequisitionsForPoAsync()
    {
        var rows = await EligibleRequisitions().OrderByDescending(m => m.MRId).Take(300)
            .Select(m => new { m.MRId, m.MRNumber, m.Description, m.ProjectId }).ToListAsync();
        return rows.Select(m => (m.MRId, m.MRNumber, m.Description, m.ProjectId)).ToList();
    }

    public async Task<IReadOnlyList<(int Id, string Code, string Description)>> GetCommodityGroupsAsync()
    {
        var rows = await _context.PartCatalogueCommodityGroups.AsNoTracking().Where(g => g.IsActive).OrderBy(g => g.Code)
            .Select(g => new { g.CommodityGroupId, g.Code, g.Description }).ToListAsync();
        return rows.Select(g => (g.CommodityGroupId, g.Code, g.Description)).ToList();
    }

    /// <summary>
    /// An MR feeds a PO when it is issued to procurement (engineering path, and its pre-procurement item is not still open) or when
    /// every item came from the procurement cartable (SCRUM-471). Closed / cancelled MRs never do.
    /// </summary>
    private IQueryable<MaterialRequisition> EligibleRequisitions() =>
        _context.MaterialRequisitions.AsNoTracking().Where(m => m.IsActive && !EndedMrStatuses.Contains(m.Status)
            && (m.Status == MrIssuedToProcurement
                || (m.Items.Any() && m.Items.All(i => i.ProcurementDemandId != null)))
            && !_context.ProcurementDemands.Any(d => d.SourceType == ProcurementDemandSources.MaterialRequisition
                                                    && d.SourceId == m.MRId && d.Status == ProcurementDemandStatuses.Open));

    public async Task<PoResult> CreateFromRequisitionAsync(int mrId, int vendorId, string currency, string? poNumber, PoActor actor)
    {
        if (!await _permissions.HasPermissionAsync(actor.Roles, Module, "Create")) return PoResult.Fail("Po_Err_Forbidden");
        var mr = await EligibleRequisitions().FirstOrDefaultAsync(m => m.MRId == mrId);
        if (mr == null) return PoResult.Fail("Po_Err_MrNotEligible");
        if (string.IsNullOrWhiteSpace(currency)) return PoResult.Fail("Po_Err_Currency");
        if (!await _context.Vendors.AnyAsync(v => v.VendorId == vendorId && v.IsActive)) return PoResult.Fail("Po_Err_Vendor");

        var free = await FreeItemsAsync(mrId);
        if (free.Count == 0) return PoResult.Fail("Po_Err_NoFreeItems");

        int poId;
        try
        {
            // construction stays in PurchaseOrderService (numbering, uniqueness, Draft); the creation-path test guards it
            poId = await _orders.CreateAsync(poNumber?.Trim() ?? string.Empty, mr.ProjectId, vendorId, null, $"From {mr.MRNumber}: {mr.Description}", null);
        }
        catch (InvalidOperationException ex)
        {
            return PoResult.Fail(ex.Message);
        }

        var po = await _context.PurchaseOrders.Include(p => p.Lines).FirstAsync(p => p.PoId == poId);
        po.MaterialRequisitionId = mrId;
        po.Currency = currency.Trim().ToUpperInvariant();
        await AddLinesAsync(po, free, mr);
        Audit(poId, "CreateFromMR", $"MR: {mr.MRNumber}\nLines: {free.Count}", actor);
        await _context.SaveChangesAsync();
        return new PoResult(true, null, poId);
    }

    public async Task<PoResult> AddLinesFromRequisitionAsync(int poId, int mrId, PoActor actor)
    {
        if (!await _permissions.HasPermissionAsync(actor.Roles, Module, "Edit")) return PoResult.Fail("Po_Err_Forbidden");
        var po = await _context.PurchaseOrders.Include(p => p.Lines).FirstOrDefaultAsync(p => p.PoId == poId && p.IsActive);
        if (po == null) return PoResult.Fail("Po_Err_NotFound");
        if (po.Status != PurchaseOrderStatuses.Draft) return PoResult.Fail("Po_Err_NotDraft");
        var mr = await EligibleRequisitions().FirstOrDefaultAsync(m => m.MRId == mrId);
        if (mr == null) return PoResult.Fail("Po_Err_MrNotEligible");
        if (mr.ProjectId != po.ProjectId) return PoResult.Fail("Po_Err_ProjectMismatch");

        var free = await FreeItemsAsync(mrId);
        if (free.Count == 0) return PoResult.Fail("Po_Err_NoFreeItems");
        po.MaterialRequisitionId ??= mrId;
        await AddLinesAsync(po, free, mr);
        Audit(poId, "AddLinesFromMR", $"MR: {mr.MRNumber}\nLines: {free.Count}", actor);
        await _context.SaveChangesAsync();
        return new PoResult(true, null, poId);
    }

    /// <summary>MR items not already on a line of a live PO (an MR item is ordered once).</summary>
    private async Task<List<MaterialRequisitionItem>> FreeItemsAsync(int mrId)
    {
        var used = await _context.PurchaseOrderLines.AsNoTracking()
            .Where(l => l.MaterialRequisitionItemId != null && l.MaterialRequisitionItem!.MRId == mrId
                        && l.PurchaseOrder!.IsActive && !DeadPoStatuses.Contains(l.PurchaseOrder.Status))
            .Select(l => l.MaterialRequisitionItemId!.Value).ToListAsync();
        return await _context.MaterialRequisitionItems.AsNoTracking()
            .Where(i => i.MRId == mrId && !used.Contains(i.MRItemId)).OrderBy(i => i.MRItemId).ToListAsync();
    }

    private async Task AddLinesAsync(PurchaseOrder po, List<MaterialRequisitionItem> items, MaterialRequisition mr)
    {
        // the awarded bid of the MR's package, when a tender awarded one (traceability MR -> tender/award -> PO)
        int? tenderId = null, bidId = null;
        if (mr.ProcurementPackageId.HasValue)
        {
            var awarded = await _context.Tenders.AsNoTracking()
                .Where(t => t.ProcurementPackageId == mr.ProcurementPackageId && t.AwardedBidId != null && t.IsActive)
                .OrderByDescending(t => t.TenderId).Select(t => new { t.TenderId, t.AwardedBidId }).FirstOrDefaultAsync();
            if (awarded != null) { tenderId = awarded.TenderId; bidId = awarded.AwardedBidId; }
        }

        var next = po.Lines.Count == 0 ? 1 : po.Lines.Max(l => l.LineNo) + 1;
        foreach (var i in items)
        {
            var catalogued = i.MaterialItemId != null || i.PartCatalogueRequestId != null;
            po.Lines.Add(new PurchaseOrderLine
            {
                LineNo = next++,
                MaterialRequisitionItemId = i.MRItemId,
                MaterialItemId = i.MaterialItemId,
                PartCatalogueRequestId = i.PartCatalogueRequestId,
                Description = i.ItemDescription.Length <= 500 ? i.ItemDescription : i.ItemDescription[..500],
                PartNumber = i.PartNumber,
                Quantity = (decimal)i.Quantity,
                Unit = i.Unit,
                UnitPrice = 0m,
                RequiredDate = mr.RequiredDate,
                IsStockLine = catalogued,          // a free-text line starts as non-stock; the buyer completes it
                TenderId = tenderId,
                BidId = bidId,
            });
        }
    }

    // ================================================================== lines in Draft

    public async Task<PoResult> UpdateLineAsync(int lineId, PoLineInput input, PoActor actor)
    {
        if (!await _permissions.HasPermissionAsync(actor.Roles, Module, "Edit")) return PoResult.Fail("Po_Err_Forbidden");
        var line = await _context.PurchaseOrderLines.Include(l => l.PurchaseOrder).FirstOrDefaultAsync(l => l.PurchaseOrderLineId == lineId);
        if (line?.PurchaseOrder == null) return PoResult.Fail("Po_Err_NotFound");
        if (line.PurchaseOrder.Status != PurchaseOrderStatuses.Draft) return PoResult.Fail("Po_Err_NotDraft");
        if (input.Quantity <= 0) return PoResult.Fail("Po_Err_Quantity");
        if (input.UnitPrice < 0) return PoResult.Fail("Po_Err_Price");

        line.Quantity = input.Quantity;
        line.UnitPrice = input.UnitPrice;
        line.RequiredDate = input.RequiredDate;
        line.IsStockLine = input.IsStockLine;
        line.CommodityGroupId = input.CommodityGroupId;
        line.FreeTextReason = string.IsNullOrWhiteSpace(input.FreeTextReason) ? null : Truncate(input.FreeTextReason.Trim(), 500);
        await _context.SaveChangesAsync();
        return new PoResult(true, null, line.PoId);
    }

    public async Task<PoResult> RemoveLineAsync(int lineId, PoActor actor)
    {
        if (!await _permissions.HasPermissionAsync(actor.Roles, Module, "Edit")) return PoResult.Fail("Po_Err_Forbidden");
        var line = await _context.PurchaseOrderLines.Include(l => l.PurchaseOrder).FirstOrDefaultAsync(l => l.PurchaseOrderLineId == lineId);
        if (line?.PurchaseOrder == null) return PoResult.Fail("Po_Err_NotFound");
        if (line.PurchaseOrder.Status != PurchaseOrderStatuses.Draft) return PoResult.Fail("Po_Err_NotDraft");
        var poId = line.PoId;
        _context.PurchaseOrderLines.Remove(line);
        Audit(poId, "RemoveLine", $"Line {line.LineNo}: {line.Description}", actor);
        await _context.SaveChangesAsync();
        return new PoResult(true, null, poId);
    }

    // ================================================================== read

    public async Task<PoApprovalView?> GetAsync(int poId)
    {
        var po = await _context.PurchaseOrders.AsNoTracking().Include(p => p.MaterialRequisition).Include(p => p.AuthorityRule)
            .Include(p => p.Lines).ThenInclude(l => l.CommodityGroup)
            .FirstOrDefaultAsync(p => p.PoId == poId);
        if (po == null) return null;
        var lines = po.Lines.OrderBy(l => l.LineNo).Select(l => new PoLineView(l.PurchaseOrderLineId, l.LineNo, l.Description, l.PartNumber, l.IsCatalogued,
            l.Quantity, l.Unit, l.UnitPrice, l.LineAmount, l.RequiredDate, l.IsStockLine, l.CommodityGroupId,
            l.CommodityGroup == null ? null : $"{l.CommodityGroup.Code} - {l.CommodityGroup.Description}",
            l.FreeTextReason, l.MaterialRequisitionItemId, l.TenderId, l.BidId)).ToList();
        return new PoApprovalView(po.PoId, po.PoNumber, po.Status, po.Currency, lines.Sum(l => l.LineAmount), po.MaterialRequisitionId,
            po.MaterialRequisition?.MRNumber, po.ApproverRole, po.AuthorityRule?.Level, po.SubmittedAt, po.SubmittedByUserName, po.ApprovedAt,
            po.ApprovedByUserName, po.RejectionReason, po.CreatedBy, lines);
    }

    // ================================================================== submit / route / approve / reject

    public async Task<PoResult> SubmitAsync(int poId, PoActor actor)
    {
        if (!await _permissions.HasPermissionAsync(actor.Roles, Module, "Edit")) return PoResult.Fail("Po_Err_Forbidden");
        var po = await _context.PurchaseOrders.Include(p => p.Lines).FirstOrDefaultAsync(p => p.PoId == poId && p.IsActive);
        if (po == null) return PoResult.Fail("Po_Err_NotFound");
        if (po.Status != PurchaseOrderStatuses.Draft) return PoResult.Fail("Po_Err_NotDraft");
        if (po.Lines.Count == 0) return PoResult.Fail("Po_Err_NoLines");
        if (string.IsNullOrWhiteSpace(po.Currency)) return PoResult.Fail("Po_Err_Currency");

        foreach (var l in po.Lines)
        {
            if (l.Quantity <= 0) return PoResult.Fail("Po_Err_Quantity");
            if (l.UnitPrice < 0) return PoResult.Fail("Po_Err_Price");
            if (!l.IsCatalogued)
            {
                if (_options.CatalogueOnlyLines) return PoResult.Fail("Po_Err_CatalogueOnly");
                if (l.IsStockLine) return PoResult.Fail("Po_Err_StockLineNotCatalogued");
                if (l.CommodityGroupId == null || string.IsNullOrWhiteSpace(l.FreeTextReason)) return PoResult.Fail("Po_Err_FreeTextIncomplete");
            }
        }

        po.Value = po.Lines.Sum(l => l.Quantity * l.UnitPrice);
        po.Status = PurchaseOrderStatuses.Submitted;
        po.SubmittedAt = DateTime.Now;
        po.SubmittedByUserName = actor.UserName;
        po.RejectionReason = null;
        po.ApprovedAt = null;
        po.ApprovedByUserName = null;
        return await RouteCoreAsync(po, actor, "Submit");
    }

    public async Task<PoResult> RouteAsync(int poId, PoActor actor)
    {
        if (!await _permissions.HasPermissionAsync(actor.Roles, Module, "Edit")) return PoResult.Fail("Po_Err_Forbidden");
        var po = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.PoId == poId && p.IsActive);
        if (po == null) return PoResult.Fail("Po_Err_NotFound");
        if (po.Status != PurchaseOrderStatuses.Submitted || !string.IsNullOrEmpty(po.ApproverRole)) return PoResult.Fail("Po_Err_AlreadyRouted");
        return await RouteCoreAsync(po, actor, "Route");
    }

    /// <summary>Lowest-level active rule of the PO currency whose amount range holds the value; none = responsible not set.</summary>
    private async Task<PoResult> RouteCoreAsync(PurchaseOrder po, PoActor actor, string action)
    {
        var value = po.Value ?? 0m;
        var currency = po.Currency!.Trim().ToUpperInvariant();
        var rule = (await _context.PurchaseOrderAuthorityRules.AsNoTracking().Where(r => r.IsActive && r.Currency == currency && r.MinAmount <= value).ToListAsync())
            .Where(r => r.MaxAmount == null || value <= r.MaxAmount.Value)
            .OrderBy(r => r.Level).ThenBy(r => r.PurchaseOrderAuthorityRuleId).FirstOrDefault();

        po.AuthorityRuleId = rule?.PurchaseOrderAuthorityRuleId;
        po.ApproverRole = rule?.ApproverRole;
        Audit(po.PoId, action, rule == null
            ? $"Value: {Money(value)} {currency}\nResponsible not set: no active PO authority rule matches"
            : $"Value: {Money(value)} {currency}\nRule: level {rule.Level} ({rule.ApproverRole})", actor);

        await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            await _context.SaveChangesAsync();
            if (rule != null)
                await _runtime.StartInstanceAsync(PurchaseOrderApprovalWorkflowSeeder.Module, PurchaseOrderApprovalWorkflowSeeder.BusinessObjectType, po.PoId,
                    PurchaseOrderApprovalWorkflowSeeder.Category, po.ProjectId, actor.EmployeeId, new[] { rule.ApproverRole });
            await tx.CommitAsync();
        });
        return rule == null ? new PoResult(true, "Po_ResponsibleNotSet", po.PoId) : new PoResult(true, null, po.PoId);
    }

    public async Task<PoResult> ApproveAsync(int poId, PoActor actor)
    {
        var po = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.PoId == poId && p.IsActive);
        if (po == null) return PoResult.Fail("Po_Err_NotFound");
        if (po.Status != PurchaseOrderStatuses.Submitted) return PoResult.Fail("Po_Err_NotSubmitted");
        if (string.IsNullOrEmpty(po.ApproverRole)) return PoResult.Fail("Po_ResponsibleNotSet");
        if (!actor.Roles.Contains(po.ApproverRole, StringComparer.OrdinalIgnoreCase)) return PoResult.Fail("Po_Err_NotApprover");
        if (IsSamePerson(actor.UserName, po.CreatedBy) || IsSamePerson(actor.UserName, po.SubmittedByUserName))
            return PoResult.Fail("Po_Err_SoD");

        po.Status = PurchaseOrderStatuses.Approved;
        po.ApprovedAt = DateTime.Now;
        po.ApprovedByUserName = actor.UserName;
        Audit(poId, "Approve", $"Role: {po.ApproverRole}\nValue: {Money(po.Value ?? 0m)} {po.Currency}", actor);
        return await DecideAsync(po, "Approve", null, actor);
    }

    public async Task<PoResult> RejectAsync(int poId, string reason, PoActor actor)
    {
        if (string.IsNullOrWhiteSpace(reason)) return PoResult.Fail("Po_Err_ReasonRequired");
        var po = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.PoId == poId && p.IsActive);
        if (po == null) return PoResult.Fail("Po_Err_NotFound");
        if (po.Status != PurchaseOrderStatuses.Submitted) return PoResult.Fail("Po_Err_NotSubmitted");
        // with a routed role only its holders reject; while the responsible is not set the buyer may take it back
        if (!string.IsNullOrEmpty(po.ApproverRole) && !actor.Roles.Contains(po.ApproverRole, StringComparer.OrdinalIgnoreCase))
            return PoResult.Fail("Po_Err_NotApprover");
        if (string.IsNullOrEmpty(po.ApproverRole) && !await _permissions.HasPermissionAsync(actor.Roles, Module, "Edit"))
            return PoResult.Fail("Po_Err_Forbidden");

        po.Status = PurchaseOrderStatuses.Draft;
        po.RejectionReason = Truncate(reason.Trim(), 1000);
        Audit(poId, "Reject", $"Reason: {po.RejectionReason}", actor);
        return await DecideAsync(po, "Reject", po.RejectionReason, actor);
    }

    private async Task<PoResult> DecideAsync(PurchaseOrder po, string action, string? comment, PoActor actor)
    {
        try
        {
            await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync();
                await _context.SaveChangesAsync();
                var instance = await _runtime.GetInstanceAsync(PurchaseOrderApprovalWorkflowSeeder.BusinessObjectType, po.PoId);
                if (instance?.Status == RunningInstance)
                    await _runtime.ExecuteActionAsync(instance.WorkflowInstanceId, action, actor.EmployeeId, actor.Roles, comment, null, null);
                await tx.CommitAsync();
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            return PoResult.Fail("Po_Err_Concurrent");
        }
        return new PoResult(true, null, po.PoId);
    }

    // ================================================================== authority table

    public async Task<IReadOnlyList<PoAuthorityRuleView>> GetRulesAsync() =>
        await _context.PurchaseOrderAuthorityRules.AsNoTracking().OrderBy(r => r.Currency).ThenBy(r => r.Level).ThenBy(r => r.MinAmount)
            .Select(r => new PoAuthorityRuleView(r.PurchaseOrderAuthorityRuleId, r.Level, r.MinAmount, r.MaxAmount, r.Currency, r.ApproverRole, r.Description, r.IsActive))
            .ToListAsync();

    public async Task<PoResult> SaveRuleAsync(PoAuthorityRuleInput input, PoActor actor)
    {
        if (string.IsNullOrWhiteSpace(input.ApproverRole) || string.IsNullOrWhiteSpace(input.Currency)) return PoResult.Fail("Po_Err_RuleIncomplete");
        if (input.MinAmount < 0 || (input.MaxAmount.HasValue && input.MaxAmount.Value < input.MinAmount)) return PoResult.Fail("Po_Err_RuleRange");
        if (!await _context.Roles.AnyAsync(r => r.Name == input.ApproverRole.Trim())) return PoResult.Fail("Po_Err_RuleRole");

        var rule = input.Id.HasValue
            ? await _context.PurchaseOrderAuthorityRules.FirstOrDefaultAsync(r => r.PurchaseOrderAuthorityRuleId == input.Id.Value)
            : null;
        if (input.Id.HasValue && rule == null) return PoResult.Fail("Po_Err_NotFound");
        if (rule == null) { rule = new PurchaseOrderAuthorityRule(); _context.PurchaseOrderAuthorityRules.Add(rule); }

        rule.Level = input.Level;
        rule.MinAmount = input.MinAmount;
        rule.MaxAmount = input.MaxAmount;
        rule.Currency = input.Currency.Trim().ToUpperInvariant();
        rule.ApproverRole = input.ApproverRole.Trim();
        rule.Description = string.IsNullOrWhiteSpace(input.Description) ? null : Truncate(input.Description.Trim(), 500);
        rule.IsActive = input.IsActive;
        await _context.SaveChangesAsync();
        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(PurchaseOrderAuthorityRule), EntityKeyValue = rule.PurchaseOrderAuthorityRuleId.ToString(CultureInfo.InvariantCulture),
            Action = input.Id.HasValue ? "Update" : "Create",
            ChangedFieldsSummary = $"Level {rule.Level}: {Money(rule.MinAmount)} - {(rule.MaxAmount.HasValue ? Money(rule.MaxAmount.Value) : "no limit")} {rule.Currency} -> {rule.ApproverRole} ({(rule.IsActive ? "active" : "inactive")})",
            UserName = actor.UserName, Timestamp = DateTime.Now
        });
        await _context.SaveChangesAsync();
        return new PoResult(true, null, rule.PurchaseOrderAuthorityRuleId);
    }

    // ================================================================== helpers

    private static bool IsSamePerson(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private void Audit(int poId, string action, string summary, PoActor actor) =>
        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(PurchaseOrder),
            EntityKeyValue = poId.ToString(CultureInfo.InvariantCulture),
            Action = action,
            ChangedFieldsSummary = summary,
            UserName = actor.UserName,
            Timestamp = DateTime.Now
        });

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
    private static string Money(decimal v) => v.ToString("#,0.##", CultureInfo.InvariantCulture);
}
