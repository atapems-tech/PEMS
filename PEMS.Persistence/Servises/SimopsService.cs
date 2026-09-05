using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق CMMS-10e: تشخیص Candidate Overlap در لحظه‌ی نمایش محاسبه می‌شود (Compute-on-read، بدون
/// اثر جانبی ذخیره‌شده) - هیچ اسکن پس‌زمینه‌ای وجود ندارد. SimopsConflict فقط با اقدام دستی
/// کاربر از روی این لیست ایجاد می‌شود. Severity/ConflictBasis یک‌بار در ایجاد محاسبه و منجمد
/// می‌شوند. هر اکشن روی یک تعارض، PermitId ارسالی را در برابر هر دو والد (PermitAId/PermitBId)
/// تایید می‌کند - طبق نیاز صریح دستورالعمل CMMS-10e برای رفع نقص مالکیت CMMS-10c.
/// </summary>
public class SimopsService : ISimopsService
{
    private readonly PEMSDbContext _context;

    private static readonly string[] ActivePermitStatuses = { "Issued", "Active", "Suspended" };

    private static readonly Dictionary<string, string[]> LegalTransitions = new()
    {
        ["Potential"] = new[] { "Confirmed", "AcceptedRisk" },
        ["Confirmed"] = new[] { "Resolved", "AcceptedRisk" },
    };

    public SimopsService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<SimopsCandidateDto>> GetCandidatesForPermitAsync(int permitId)
    {
        var permit = await _context.Permits.FirstOrDefaultAsync(x => x.PermitId == permitId && x.IsActive);
        if (permit == null) return new List<SimopsCandidateDto>();

        if (permit.EquipmentId == null && permit.FunctionalLocationId == null)
            return new List<SimopsCandidateDto>();

        var others = await _context.Permits
            .Include(x => x.Equipment)
            .Include(x => x.FunctionalLocation)
            .Where(x => x.PermitId != permitId && x.IsActive && ActivePermitStatuses.Contains(x.Status)
                && ((permit.EquipmentId != null && x.EquipmentId == permit.EquipmentId)
                    || (permit.FunctionalLocationId != null && x.FunctionalLocationId == permit.FunctionalLocationId)))
            .ToListAsync();

        if (others.Count == 0) return new List<SimopsCandidateDto>();

        var otherIds = others.Select(x => x.PermitId).ToList();
        var existingConflicts = await _context.SimopsConflicts
            .Where(x => x.IsActive && ((x.PermitAId == permitId && otherIds.Contains(x.PermitBId))
                || (x.PermitBId == permitId && otherIds.Contains(x.PermitAId))))
            .ToListAsync();

        var result = new List<SimopsCandidateDto>();
        foreach (var other in others)
        {
            var sameEquipment = permit.EquipmentId != null && other.EquipmentId == permit.EquipmentId;
            var sameLocation = permit.FunctionalLocationId != null && other.FunctionalLocationId == permit.FunctionalLocationId;
            var basis = sameEquipment && sameLocation ? "Same Equipment + Same Functional Location"
                : sameEquipment ? "Same Equipment" : "Same Functional Location";

            var timeWindowKnown = permit.ValidFrom.HasValue && permit.ValidTo.HasValue
                && other.ValidFrom.HasValue && other.ValidTo.HasValue;
            var timeOverlap = timeWindowKnown
                && permit.ValidFrom!.Value <= other.ValidTo!.Value
                && other.ValidFrom!.Value <= permit.ValidTo!.Value;

            var existing = existingConflicts.FirstOrDefault(c =>
                (c.PermitAId == permitId && c.PermitBId == other.PermitId) ||
                (c.PermitBId == permitId && c.PermitAId == other.PermitId));

            result.Add(new SimopsCandidateDto
            {
                PermitId = other.PermitId,
                PermitNumber = other.PermitNumber,
                Title = other.Title,
                PermitType = other.PermitType,
                Status = other.Status,
                EquipmentTagNumber = other.Equipment?.TagNumber,
                FunctionalLocationName = other.FunctionalLocation?.Name,
                ValidFrom = other.ValidFrom,
                ValidTo = other.ValidTo,
                MatchBasis = basis,
                TimeWindowKnown = timeWindowKnown,
                TimeOverlapConfirmed = timeOverlap,
                ExistingConflictId = existing?.SimopsConflictId,
                ExistingConflictStatus = existing?.Status
            });
        }

        return result;
    }

    public async Task<List<SimopsCandidateWorkOrderDto>> GetCandidateWorkOrdersForPermitAsync(int permitId)
    {
        var permit = await _context.Permits.FirstOrDefaultAsync(x => x.PermitId == permitId && x.IsActive);
        if (permit == null || permit.EquipmentId == null) return new List<SimopsCandidateWorkOrderDto>();

        var linkedWorkOrderIds = await _context.Permits
            .Where(x => x.IsActive && x.WorkOrderId != null)
            .Select(x => x.WorkOrderId!.Value)
            .ToListAsync();

        var workOrders = await _context.WorkOrders
            .Include(x => x.Equipment)
            .Where(x => x.EquipmentId == permit.EquipmentId && !linkedWorkOrderIds.Contains(x.WorkOrderId)
                && x.Status != "Completed" && x.Status != "Closed")
            .ToListAsync();

        return workOrders.Select(w => new SimopsCandidateWorkOrderDto
        {
            WorkOrderId = w.WorkOrderId,
            WorkOrderNumber = w.WorkOrderNumber,
            EquipmentTagNumber = w.Equipment?.TagNumber,
            StartDate = w.StartDate,
            CompletionDate = w.CompletionDate
        }).ToList();
    }

    public async Task<List<SimopsConflictListDto>> GetConflictsForPermitAsync(int permitId)
    {
        var conflicts = await _context.SimopsConflicts
            .Include(x => x.PermitA)
            .Include(x => x.PermitB)
            .Include(x => x.RaisedByEmployee)
            .Include(x => x.AssessedByEmployee)
            .Include(x => x.ResolvedByEmployee)
            .Include(x => x.AcceptedByEmployee)
            .Where(x => x.IsActive && (x.PermitAId == permitId || x.PermitBId == permitId))
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        var conflictIds = conflicts.Select(x => x.SimopsConflictId).ToList();
        var histories = await _context.SimopsConflictStatusHistories
            .Include(x => x.ChangedBy)
            .Where(x => conflictIds.Contains(x.SimopsConflictId))
            .OrderByDescending(x => x.ChangedDate)
            .ToListAsync();

        static string? FullName(Employee? e) => e != null ? $"{e.FirstName} {e.LastName}" : null;

        return conflicts.Select(c => new SimopsConflictListDto
        {
            SimopsConflictId = c.SimopsConflictId,
            PermitAId = c.PermitAId,
            PermitANumber = c.PermitA?.PermitNumber ?? "",
            PermitBId = c.PermitBId,
            PermitBNumber = c.PermitB?.PermitNumber ?? "",
            ConflictBasis = c.ConflictBasis,
            Severity = c.Severity,
            Status = c.Status,
            RaisedByName = FullName(c.RaisedByEmployee),
            AssessedByName = FullName(c.AssessedByEmployee),
            AssessedDate = c.AssessedDate,
            ResolvedByName = FullName(c.ResolvedByEmployee),
            ResolvedDate = c.ResolvedDate,
            ResolutionNotes = c.ResolutionNotes,
            AcceptedByName = FullName(c.AcceptedByEmployee),
            AcceptedDate = c.AcceptedDate,
            AcceptanceJustification = c.AcceptanceJustification,
            StatusHistory = histories.Where(h => h.SimopsConflictId == c.SimopsConflictId)
                .Select(h => new SimopsConflictStatusHistoryDto
                {
                    FromStatus = h.FromStatus,
                    ToStatus = h.ToStatus,
                    ChangedByName = FullName(h.ChangedBy),
                    ChangedDate = h.ChangedDate,
                    Reason = h.Reason
                }).ToList()
        }).ToList();
    }

    public async Task<(bool Success, string? Error, int? Id)> CreateAsync(int permitAId, int permitBId, int? raisedByEmployeeId)
    {
        if (permitAId == permitBId) return (false, "دو Permit باید متفاوت باشند.", null);

        var permitA = await _context.Permits.FirstOrDefaultAsync(x => x.PermitId == permitAId && x.IsActive);
        var permitB = await _context.Permits.FirstOrDefaultAsync(x => x.PermitId == permitBId && x.IsActive);
        if (permitA == null || permitB == null) return (false, "یکی از Permit ها پیدا نشد.", null);

        var alreadyExists = await _context.SimopsConflicts.AnyAsync(x => x.IsActive
            && ((x.PermitAId == permitAId && x.PermitBId == permitBId) || (x.PermitAId == permitBId && x.PermitBId == permitAId)));
        if (alreadyExists) return (false, "تعارضی برای این جفت Permit از قبل ثبت شده.", null);

        var severity = await ComputeSeverityAsync(permitA.PermitType, permitB.PermitType);

        var sameEquipment = permitA.EquipmentId != null && permitA.EquipmentId == permitB.EquipmentId;
        var sameLocation = permitA.FunctionalLocationId != null && permitA.FunctionalLocationId == permitB.FunctionalLocationId;
        var basis = sameEquipment && sameLocation ? "Same Equipment + Same Functional Location"
            : sameEquipment ? "Same Equipment" : sameLocation ? "Same Functional Location" : "Manually raised";

        var conflict = new SimopsConflict
        {
            PermitAId = permitAId,
            PermitBId = permitBId,
            ConflictBasis = basis,
            Severity = severity,
            Status = "Potential",
            RaisedByEmployeeId = raisedByEmployeeId ?? 0,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.SimopsConflicts.AddAsync(conflict);
        await _context.SaveChangesAsync();

        await _context.SimopsConflictStatusHistories.AddAsync(new SimopsConflictStatusHistory
        {
            SimopsConflictId = conflict.SimopsConflictId,
            FromStatus = "",
            ToStatus = "Potential",
            ChangedById = raisedByEmployeeId,
            ChangedDate = DateTime.Now,
            Reason = "ثبت اولیه‌ی تعارض"
        });
        await _context.SaveChangesAsync();

        return (true, null, conflict.SimopsConflictId);
    }

    public Task<(bool Success, string? Error)> ConfirmAsync(int conflictId, int permitId, string? reason, int? actorEmployeeId) =>
        AdvanceStatusAsync(conflictId, permitId, "Confirmed", reason, actorEmployeeId, c =>
        {
            c.AssessedByEmployeeId = actorEmployeeId;
            c.AssessedDate = DateTime.Now;
        });

    public Task<(bool Success, string? Error)> ResolveAsync(int conflictId, int permitId, string resolutionNotes, int? actorEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(resolutionNotes)) return Task.FromResult<(bool Success, string? Error)>((false, "توضیح رفع تعارض الزامی است."));
        return AdvanceStatusAsync(conflictId, permitId, "Resolved", resolutionNotes, actorEmployeeId, c =>
        {
            c.ResolvedByEmployeeId = actorEmployeeId;
            c.ResolvedDate = DateTime.Now;
            c.ResolutionNotes = resolutionNotes;
        });
    }

    public Task<(bool Success, string? Error)> AcceptRiskAsync(int conflictId, int permitId, string justification, int? actorEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(justification)) return Task.FromResult<(bool Success, string? Error)>((false, "دلیل پذیرش ریسک الزامی است."));
        return AdvanceStatusAsync(conflictId, permitId, "AcceptedRisk", justification, actorEmployeeId, c =>
        {
            c.AcceptedByEmployeeId = actorEmployeeId;
            c.AcceptedDate = DateTime.Now;
            c.AcceptanceJustification = justification;
        });
    }

    private async Task<(bool Success, string? Error)> AdvanceStatusAsync(
        int conflictId, int permitId, string nextStatus, string? reason, int? actorEmployeeId, Action<SimopsConflict> apply)
    {
        var conflict = await _context.SimopsConflicts.FirstOrDefaultAsync(x => x.SimopsConflictId == conflictId);
        if (conflict == null) return (false, "تعارض پیدا نشد.");

        // طبق CMMS-10e بند ۴/۹ دستورالعمل: اعتبارسنجی مالکیت سمت سرور در برابر هر دو والد -
        // رفع صریح نقص شناخته‌شده‌ی IsolationController در CMMS-10c.
        if (conflict.PermitAId != permitId && conflict.PermitBId != permitId)
            return (false, "این تعارض متعلق به این Permit نیست.");

        var fromStatus = conflict.Status;
        if (!LegalTransitions.TryGetValue(fromStatus, out var allowed) || !allowed.Contains(nextStatus))
            return (false, $"گذار از وضعیت «{fromStatus}» به «{nextStatus}» مجاز نیست.");

        apply(conflict);
        conflict.Status = nextStatus;

        await _context.SimopsConflictStatusHistories.AddAsync(new SimopsConflictStatusHistory
        {
            SimopsConflictId = conflict.SimopsConflictId,
            FromStatus = fromStatus,
            ToStatus = nextStatus,
            ChangedById = actorEmployeeId,
            ChangedDate = DateTime.Now,
            Reason = reason
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    private async Task<string> ComputeSeverityAsync(string permitTypeA, string permitTypeB)
    {
        var rule = await _context.SimopsInteractionRules.Where(x => x.IsActive
            && ((x.PermitTypeA == permitTypeA && x.PermitTypeB == permitTypeB)
                || (x.PermitTypeA == permitTypeB && x.PermitTypeB == permitTypeA)))
            .FirstOrDefaultAsync();

        return rule?.Severity ?? "Unknown";
    }
}
