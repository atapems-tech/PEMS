using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-817: the reads of the permit details page (formerly PermitController.Details). Every read checks that the permit exists (an unknown permit gives no rows) and nothing changes data.</summary>
public sealed class PermitDetailsOperations(IPermitService permits, IRoleModuleActionService actions, ICurrentUserService currentUser, IWorkflowRuntime runtime, IIsolationService isolation,
    IGasTestService gasTests, ISimopsService simops, IExecutionGateService gate, IPermitAuthorityService authority, PEMSDbContext context) : IPermitDetailsOperations
{
    private const string BusinessObjectType = PEMS.Persistence.Seed.PermitApprovalWorkflowSeeder.BusinessObjectType;

    // Draft to Submitted and Approved to Issued; the step to Active only goes through the execution gate
    private static readonly Dictionary<string, string> NextStatusMap = new() { ["Draft"] = "Submitted", ["Approved"] = "Issued", ["Issued"] = "Active", ["Suspended"] = "Active" };

    private static string Text(string? s) => s ?? string.Empty;

    private async Task<int?> EmployeeIdAsync() =>
        currentUser.UserId.HasValue ? await context.Users.Where(u => u.Id == currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync() : null;

    private bool RoleMatchesMe(string? assignedRoles) =>
        assignedRoles != null && assignedRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(r => currentUser.Roles.Contains(r));

    private static string Reasons(ExecutionGateResultDto eligibility) => string.Join("; ", eligibility.Checks.Where(c => !c.Passed).Select(c => c.Detail));

    public async Task<List<PermitHeadRow>> HeadAsync(int id)
    {
        var p = await permits.GetDetailsAsync(id);
        if (p is null) return [];
        var next = NextStatusMap.TryGetValue(p.Status, out var n) && n != "Active" ? n : string.Empty;
        var instance = await runtime.GetInstanceAsync(BusinessObjectType, id);
        var userId = currentUser.UserId ?? 0;
        var employeeId = await EmployeeIdAsync();
        var roles = currentUser.Roles.ToList();

        // the authority and competency engine is asked only for the actions the page can show, and only to explain a refusal before the click (the commands check it again)
        var notices = new List<string>();
        var canRespond = instance is { Status: "Running" } && instance.PendingStages.Any(s => RoleMatchesMe(s.AssignedToRole));
        if (canRespond)
        {
            var approve = await authority.EvaluateApproverEligibilityAsync(userId, employeeId, roles, id, "Permit", "Approve");
            var reject = await authority.EvaluateApproverEligibilityAsync(userId, employeeId, roles, id, "Permit", "Reject");
            if (approve.Result != "Eligible") notices.Add("Approve: " + Reasons(approve));
            if (reject.Result != "Eligible") notices.Add("Reject: " + Reasons(reject));
        }
        if (p.Status == "Active")
        {
            var close = await authority.EvaluateApproverEligibilityAsync(userId, employeeId, roles, id, "Permit", "Close");
            if (close.Result != "Eligible") notices.Add("Close: " + Reasons(close));
        }
        var points = await isolation.GetForPermitAsync(id);
        if (points.Any(x => x.Status == "Isolated"))
        {
            var verify = await authority.EvaluateApproverEligibilityAsync(userId, employeeId, roles, id, "LOTO", "Verify");
            if (verify.Result != "Eligible") notices.Add("LOTO verify: " + Reasons(verify));
        }
        var conflicts = await simops.GetConflictsForPermitAsync(id);
        foreach (var (status, action) in new[] { ("Potential", "Confirm"), ("Confirmed", "Resolve") })
            if (conflicts.Any(c => c.Status == status))
            {
                var r = await authority.EvaluateApproverEligibilityAsync(userId, employeeId, roles, id, "Simops", action);
                if (r.Result != "Eligible") notices.Add("SIMOPS " + action + ": " + Reasons(r));
            }
        if (conflicts.Any(c => c.Status is "Potential" or "Confirmed"))
        {
            var r = await authority.EvaluateApproverEligibilityAsync(userId, employeeId, roles, id, "Simops", "AcceptRisk");
            if (r.Result != "Eligible") notices.Add("SIMOPS accept risk: " + Reasons(r));
        }

        var gateEligible = p.Status is "Issued" or "Suspended" && (await gate.EvaluateAsync(id)).Result == "Eligible";
        var canOverride = p.Status is "Issued" or "Suspended" && await actions.HasActionAsync(currentUser.Roles, "ExecutionGate", "EmergencyOverride");
        return [new PermitHeadRow(p.PermitId, p.PermitNumber, p.Title, p.PermitType, p.Status, p.Priority, Text(p.Description), Text(p.EquipmentTagNumber), Text(p.FunctionalLocationBreadcrumb), Text(p.WorkOrderNumber),
            p.RequestedByName, Text(p.IssuerName), Text(p.ReceiverName) + (string.IsNullOrWhiteSpace(p.ReceiverPersonnelType) || p.ReceiverPersonnelType == "Internal" ? "" : " (" + p.ReceiverPersonnelType + (string.IsNullOrWhiteSpace(p.ReceiverVendorName) ? "" : ", " + p.ReceiverVendorName) + ")"),
            p.ValidFrom, p.ValidTo, p.ClosedDate, Text(p.CancellationReason), Text(p.RevocationReason), next, p.Status == "Submitted" && instance == null, canRespond, gateEligible, canOverride, string.Join(Environment.NewLine, notices))];
    }

    public async Task<List<PermitApprovalRow>> ApprovalAsync(int id)
    {
        var instance = await runtime.GetInstanceAsync(BusinessObjectType, id);
        if (instance is null) return [];
        return [new PermitApprovalRow(instance.WorkflowInstanceId, Text(instance.CurrentStageName), instance.Status,
            string.Join(" / ", instance.PendingStages.Select(s => s.AssignedToRole).Where(r => !string.IsNullOrWhiteSpace(r))), instance.StartDate, instance.CompletionDate)];
    }

    public async Task<List<PermitApprovalHistoryRow>> ApprovalHistoryAsync(int id)
    {
        var instance = await runtime.GetInstanceAsync(BusinessObjectType, id);
        if (instance is null) return [];
        return (await runtime.GetHistoryAsync(instance.WorkflowInstanceId)).Select(h => new PermitApprovalHistoryRow(h.ActionDate, h.ActionType, Text(h.FromStageName), Text(h.ToStageName), Text(h.Decision), Text(h.Role), Text(h.Comment))).ToList();
    }

    public async Task<List<PermitIsolationRow>> IsolationAsync(int id)
    {
        var p = await permits.GetDetailsAsync(id);
        var open = p is not null && p.Status is not ("Closed" or "Cancelled" or "Rejected" or "Revoked");
        return (await isolation.GetForPermitAsync(id)).Select(x => new PermitIsolationRow(x.IsolationPointId, x.PermitId, x.EnergyType, x.Location, Text(x.RequiredIsolationState), Text(x.IsolationMethod), Text(x.LockNumber), Text(x.TagNumber),
            x.Status, Text(x.EquipmentTagNumber), Text(x.IsolatedByName), x.IsolatedDate, Text(x.VerifiedByName), x.VerifiedDate, open)).ToList();
    }

    public async Task<List<PermitGasTestRow>> GasTestsAsync(int id) =>
        (await gasTests.GetForPermitAsync(id)).Select(g => new PermitGasTestRow(g.GasTestId, g.PermitId, g.Location, g.TestedAt, Text(g.TesterName),
            string.IsNullOrWhiteSpace(g.InstrumentTag) ? "" : g.InstrumentTag, g.InstrumentCalibrationStatus, g.Status, g.ValidUntil, g.IsExpired,
            string.Join("; ", g.Readings.Select(r => $"{r.Parameter} {r.Value} {r.Unit} ({r.Result})")), Text(g.InvalidationReason), g.Status != "Invalidated")).ToList();

    public async Task<List<PermitSimopsConflictRow>> SimopsConflictsAsync(int id) =>
        (await simops.GetConflictsForPermitAsync(id)).Select(c =>
        {
            var otherIsB = c.PermitAId == id;
            var notes = c.Status == "Resolved" ? Text(c.ResolutionNotes) : c.AcceptedDate.HasValue ? Text(c.AcceptanceJustification) : "";
            return new PermitSimopsConflictRow(c.SimopsConflictId, id, otherIsB ? c.PermitBNumber : c.PermitANumber, otherIsB ? c.PermitBId : c.PermitAId, c.ConflictBasis, c.Severity, c.Status, Text(c.RaisedByName), notes);
        }).ToList();

    public async Task<List<PermitSimopsCandidateRow>> SimopsCandidatesAsync(int id) =>
        (await simops.GetCandidatesForPermitAsync(id)).Select(c => new PermitSimopsCandidateRow(c.PermitId, id, c.PermitNumber, c.Title, c.PermitType, c.Status, Text(c.EquipmentTagNumber), c.MatchBasis,
            c.TimeWindowKnown ? (c.TimeOverlapConfirmed ? "Overlap confirmed" : "No overlap") : "Unknown", c.ExistingConflictId != null)).ToList();

    public async Task<List<PermitCandidateWorkOrderRow>> CandidateWorkOrdersAsync(int id) =>
        (await simops.GetCandidateWorkOrdersForPermitAsync(id)).Select(w => new PermitCandidateWorkOrderRow(w.WorkOrderId, w.WorkOrderNumber, Text(w.EquipmentTagNumber), w.StartDate, w.CompletionDate)).ToList();

    public async Task<List<PermitHandoverRow>> HandoversAsync(int id) =>
        (await context.ShiftHandoverItems.AsNoTracking().Include(x => x.ShiftHandover).Where(x => x.ReferenceType == "Permit" && x.ReferenceId == id)
            .Select(x => new { x.ShiftHandoverId, x.ShiftHandover!.Status, x.ShiftHandover.PreparedDate }).Distinct().ToListAsync())
        .Select(h => new PermitHandoverRow(h.ShiftHandoverId, h.ShiftHandoverId, h.Status, h.PreparedDate)).ToList();

    public async Task<List<PermitGateCheckRow>> GateChecksAsync(int id)
    {
        var p = await permits.GetDetailsAsync(id);
        if (p is null || p.Status is not ("Issued" or "Suspended")) return [];
        var result = await gate.EvaluateAsync(id);
        var i = 0;
        return result.Checks.Select(c => new PermitGateCheckRow(++i, c.Category, c.Name, c.Passed, c.Detail, false))
            .Concat(result.AdvisoryNotes.Select(a => new PermitGateCheckRow(++i, a.Category, a.Name, a.Passed, a.Detail, true))).ToList();
    }

    public async Task<List<PermitGateDecisionRow>> GateDecisionsAsync(int id)
    {
        var i = 0;
        return (await gate.GetDecisionsForPermitAsync(id)).Select(d => new PermitGateDecisionRow(++i, d.FromStatus, d.ToStatus, d.Result, Text(d.ActorName), d.DecisionDate, d.IsEmergencyOverride,
            Text(d.IsEmergencyOverride ? d.EmergencyReason : d.BlockingReasonsSummary))).ToList();
    }

    public async Task<List<PermitStatusHistoryRow>> StatusHistoryAsync(int id)
    {
        var p = await permits.GetDetailsAsync(id);
        if (p is null) return [];
        var i = 0;
        return p.StatusHistory.Select(h => new PermitStatusHistoryRow(++i, h.FromStatus, h.ToStatus, Text(h.ChangedByName), h.ChangedDate, Text(h.Reason))).ToList();
    }
}
