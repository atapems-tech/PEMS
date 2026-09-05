using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق CMMS-10f: Execution Gate یک تابع محاسبه‌شده در لحظه است - هرگز به‌عنوان Permit.Status
/// ذخیره نمی‌شود (بند ۴ پلن). هیچ منطق اعتبارسنجی LOTO/GasTest/SIMOPS این‌جا تکرار نمی‌شود -
/// فقط از جداول موجود آن‌ها Query می‌گیرد. گذار واقعی Issued/Suspended→Active از طریق همون
/// IPermitService.AdvanceStatusAsync موجود انجام می‌شود - نه یک مسیر گذار موازی.
/// </summary>
public class ExecutionGateService : IExecutionGateService
{
    private readonly PEMSDbContext _context;
    private readonly IPermitService _permitService;

    public ExecutionGateService(PEMSDbContext context, IPermitService permitService)
    {
        _context = context;
        _permitService = permitService;
    }

    public async Task<ExecutionGateResultDto> EvaluateAsync(int permitId)
    {
        var result = new ExecutionGateResultDto();
        var permit = await _context.Permits.FirstOrDefaultAsync(x => x.PermitId == permitId && x.IsActive);
        if (permit == null)
        {
            result.Checks.Add(new ExecutionGateCheckDto { Name = "Permit", Category = "Required", Passed = false, Detail = "Permit پیدا نشد." });
            result.Result = "Blocked";
            return result;
        }

        var now = DateTime.Now;

        var statusOk = permit.Status is "Issued" or "Suspended";
        result.Checks.Add(new ExecutionGateCheckDto
        {
            Name = "Permit Status",
            Category = "Required",
            Passed = statusOk,
            Detail = statusOk ? $"وضعیت: {permit.Status}" : $"وضعیت «{permit.Status}» برای اجرا مناسب نیست - باید Issued یا Suspended باشد."
        });

        var expired = permit.ValidTo.HasValue && permit.ValidTo.Value < now;
        result.Checks.Add(new ExecutionGateCheckDto
        {
            Name = "Permit Not Expired",
            Category = "Required",
            Passed = !expired,
            Detail = expired ? $"Permit در {permit.ValidTo:yyyy-MM-dd HH:mm} منقضی شده." : "منقضی نشده."
        });

        var simopsConflicts = await _context.SimopsConflicts
            .Where(x => x.IsActive && (x.PermitAId == permitId || x.PermitBId == permitId))
            .ToListAsync();
        var highUnresolved = simopsConflicts.Where(x => x.Severity == "High" && x.Status is "Potential" or "Confirmed").ToList();
        result.Checks.Add(new ExecutionGateCheckDto
        {
            Name = "No Unresolved High SIMOPS",
            Category = "Required",
            Passed = highUnresolved.Count == 0,
            Detail = highUnresolved.Count == 0 ? "تعارض شدید حل‌نشده‌ای وجود ندارد." : $"{highUnresolved.Count} تعارض SIMOPS با شدت High حل‌نشده."
        });

        var otherUnresolved = simopsConflicts.Where(x => x.Severity != "High" && x.Status is "Potential" or "Confirmed").ToList();
        if (otherUnresolved.Count > 0)
        {
            result.AdvisoryNotes.Add(new ExecutionGateCheckDto
            {
                Name = "Other Unresolved SIMOPS",
                Category = "Advisory",
                Passed = false,
                Detail = $"{otherUnresolved.Count} تعارض SIMOPS با شدت پایین‌تر هنوز حل نشده."
            });
        }

        var requirement = await _context.PermitExecutionRequirements
            .FirstOrDefaultAsync(x => x.IsActive && x.PermitType == permit.PermitType);

        if (requirement?.RequireIsolationVerified == true)
        {
            var points = await _context.IsolationPoints
                .Where(x => x.IsActive && x.PermitId == permitId && x.Status != "Cancelled")
                .ToListAsync();
            var allVerified = points.Count > 0 && points.All(p => p.Status == "Verified");
            result.Checks.Add(new ExecutionGateCheckDto
            {
                Name = "Isolation Verified",
                Category = "Conditional",
                Passed = allVerified,
                Detail = points.Count == 0 ? "ایزوله‌سازی برای این نوع Permit الزامی است ولی نقطه‌ای ثبت نشده."
                    : allVerified ? $"همه‌ی {points.Count} نقطه‌ی ایزوله‌سازی Verified است."
                    : "همه‌ی نقاط ایزوله‌سازی هنوز Verified نیستند."
            });
        }

        if (requirement?.RequireValidGasTest == true)
        {
            var latestTest = await _context.GasTests
                .Where(x => x.IsActive && x.PermitId == permitId && x.Status != "Invalidated")
                .OrderByDescending(x => x.TestedAt)
                .FirstOrDefaultAsync();
            var isExpired = latestTest?.ValidUntil.HasValue == true && latestTest.ValidUntil.Value < now;
            var ok = latestTest != null && latestTest.Status == "Passed" && !isExpired;
            result.Checks.Add(new ExecutionGateCheckDto
            {
                Name = "Valid Gas Test",
                Category = "Conditional",
                Passed = ok,
                Detail = latestTest == null ? "تست گاز برای این نوع Permit الزامی است ولی ثبت نشده."
                    : latestTest.Status != "Passed" ? $"آخرین تست گاز «{latestTest.Status}» است."
                    : isExpired ? "آخرین تست گاز منقضی شده است."
                    : "آخرین تست گاز معتبر است."
            });
        }

        result.Result = result.Checks.All(c => c.Passed) ? "Eligible" : "Blocked";
        return result;
    }

    public async Task<List<ExecutionGateDecisionDto>> GetDecisionsForPermitAsync(int permitId)
    {
        var decisions = await _context.ExecutionGateDecisions
            .Include(x => x.ActorEmployee)
            .Where(x => x.PermitId == permitId)
            .OrderByDescending(x => x.DecisionDate)
            .ToListAsync();

        return decisions.Select(d => new ExecutionGateDecisionDto
        {
            FromStatus = d.FromStatus,
            ToStatus = d.ToStatus,
            Result = d.Result,
            ActorName = d.ActorEmployee != null ? $"{d.ActorEmployee.FirstName} {d.ActorEmployee.LastName}" : null,
            BlockingReasonsSummary = d.BlockingReasonsSummary,
            IsEmergencyOverride = d.IsEmergencyOverride,
            EmergencyReason = d.EmergencyReason,
            DecisionDate = d.DecisionDate
        }).ToList();
    }

    public async Task<(bool Success, string? Error)> StartWorkAsync(int permitId, int? actorEmployeeId)
    {
        var permit = await _context.Permits.FirstOrDefaultAsync(x => x.PermitId == permitId);
        if (permit == null) return (false, "Permit پیدا نشد.");

        var fromStatus = permit.Status;
        var evaluation = await EvaluateAsync(permitId);

        if (evaluation.Result != "Eligible")
        {
            var reasons = string.Join("؛ ", evaluation.Checks.Where(c => !c.Passed).Select(c => c.Detail));

            await _context.ExecutionGateDecisions.AddAsync(new ExecutionGateDecision
            {
                PermitId = permitId,
                ActorEmployeeId = actorEmployeeId,
                FromStatus = fromStatus,
                ToStatus = fromStatus,
                Result = "Blocked",
                BlockingReasonsSummary = reasons,
                IsEmergencyOverride = false,
                DecisionDate = DateTime.Now
            });
            await _context.SaveChangesAsync();

            return (false, $"Execution Gate: BLOCKED — {reasons}");
        }

        var (success, error) = await _permitService.AdvanceStatusAsync(permitId, "Active", "Execution Gate: ELIGIBLE - Start Work", actorEmployeeId);
        if (!success) return (success, error);

        await _context.ExecutionGateDecisions.AddAsync(new ExecutionGateDecision
        {
            PermitId = permitId,
            ActorEmployeeId = actorEmployeeId,
            FromStatus = fromStatus,
            ToStatus = "Active",
            Result = "Eligible",
            BlockingReasonsSummary = null,
            IsEmergencyOverride = false,
            DecisionDate = DateTime.Now
        });
        await _context.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> EmergencyOverrideAsync(
        int permitId, string reason, int? actorEmployeeId, IReadOnlyList<string> actorRoles)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return (false, "دلیل Emergency Override الزامی است.");

        var permit = await _context.Permits.FirstOrDefaultAsync(x => x.PermitId == permitId);
        if (permit == null) return (false, "Permit پیدا نشد.");

        var fromStatus = permit.Status;

        // طبق بند ۷ پلن: Emergency Override فقط چک‌های Conditional/SIMOPS High را Bypass می‌کند -
        // نه پیش‌شرط‌های ساختاری (وضعیت Issued/Suspended، عدم انقضا). این عمداً است.
        if (permit.Status is not ("Issued" or "Suspended"))
            return (false, $"وضعیت «{permit.Status}» برای Emergency Override هم مناسب نیست - باید Issued یا Suspended باشد.");

        var now = DateTime.Now;
        if (permit.ValidTo.HasValue && permit.ValidTo.Value < now)
            return (false, "Permit منقضی شده - Emergency Override نمی‌تواند انقضای Permit را نادیده بگیرد.");

        var evaluation = await EvaluateAsync(permitId);
        var bypassedReasons = evaluation.Checks.Where(c => !c.Passed).Select(c => c.Detail).ToList();
        var reasonsSummary = bypassedReasons.Count > 0
            ? string.Join("؛ ", bypassedReasons)
            : "هیچ‌کدام (Gate از قبل Eligible بود - Override واقعاً لازم نبوده).";

        var (success, error) = await _permitService.AdvanceStatusAsync(
            permitId, "Active", $"Emergency Override: {reason}", actorEmployeeId);
        if (!success) return (success, error);

        await _context.ExecutionGateDecisions.AddAsync(new ExecutionGateDecision
        {
            PermitId = permitId,
            ActorEmployeeId = actorEmployeeId,
            FromStatus = fromStatus,
            ToStatus = "Active",
            Result = "Eligible",
            BlockingReasonsSummary = reasonsSummary,
            IsEmergencyOverride = true,
            EmergencyReason = reason,
            DecisionDate = DateTime.Now
        });
        await _context.SaveChangesAsync();

        return (true, null);
    }
}
