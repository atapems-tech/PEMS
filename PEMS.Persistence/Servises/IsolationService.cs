using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق CMMS-10c: چرخه‌ی وضعیت IsolationPoint - Requested→Isolated→Verified→Deisolated، به‌علاوه
/// گذار استثنایی Requested→Cancelled. دقیقاً همون الگوی PermitService/WorkRequestService؛
/// بدون نیاز به IWorkflowRuntime (تایید تک‌بازیگر، نه زنجیره‌ی چندنقشی).
/// </summary>
public class IsolationService : IIsolationService
{
    private readonly PEMSDbContext _context;

    public static readonly string[] ValidEnergyTypes =
    {
        "Electrical", "Mechanical", "Hydraulic", "Pneumatic", "Pressure", "Steam",
        "Thermal", "Chemical", "Gravity", "StoredEnergy", "ProcessEnergy"
    };

    private static readonly Dictionary<string, string[]> LegalTransitions = new()
    {
        ["Requested"] = new[] { "Isolated", "Cancelled" },
        ["Isolated"] = new[] { "Verified" },
        ["Verified"] = new[] { "Deisolated" },
    };

    public IsolationService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<IsolationPointListDto>> GetForPermitAsync(int permitId)
    {
        var points = await _context.IsolationPoints
            .Include(x => x.Equipment)
            .Include(x => x.IsolatedByEmployee)
            .Include(x => x.VerifiedByEmployee)
            .Include(x => x.DeisolatedByEmployee)
            .Where(x => x.PermitId == permitId && x.IsActive)
            .OrderBy(x => x.CreatedDate)
            .ToListAsync();

        var pointIds = points.Select(x => x.IsolationPointId).ToList();

        var histories = await _context.IsolationStatusHistories
            .Include(x => x.ChangedBy)
            .Where(x => pointIds.Contains(x.IsolationPointId))
            .OrderByDescending(x => x.ChangedDate)
            .ToListAsync();

        return points.Select(p => new IsolationPointListDto
        {
            IsolationPointId = p.IsolationPointId,
            PermitId = p.PermitId,
            EnergyType = p.EnergyType,
            Location = p.Location,
            NormalState = p.NormalState,
            RequiredIsolationState = p.RequiredIsolationState,
            IsolationMethod = p.IsolationMethod,
            LockNumber = p.LockNumber,
            TagNumber = p.TagNumber,
            Status = p.Status,
            EquipmentTagNumber = p.Equipment?.TagNumber,
            IsolatedByName = p.IsolatedByEmployee != null ? $"{p.IsolatedByEmployee.FirstName} {p.IsolatedByEmployee.LastName}" : null,
            IsolatedDate = p.IsolatedDate,
            VerifiedByName = p.VerifiedByEmployee != null ? $"{p.VerifiedByEmployee.FirstName} {p.VerifiedByEmployee.LastName}" : null,
            VerifiedDate = p.VerifiedDate,
            DeisolatedByName = p.DeisolatedByEmployee != null ? $"{p.DeisolatedByEmployee.FirstName} {p.DeisolatedByEmployee.LastName}" : null,
            DeisolatedDate = p.DeisolatedDate,
            StatusHistory = histories.Where(h => h.IsolationPointId == p.IsolationPointId)
                .Select(h => new IsolationStatusHistoryDto
                {
                    FromStatus = h.FromStatus,
                    ToStatus = h.ToStatus,
                    ChangedByName = h.ChangedBy != null ? $"{h.ChangedBy.FirstName} {h.ChangedBy.LastName}" : null,
                    ChangedDate = h.ChangedDate,
                    Reason = h.Reason
                }).ToList()
        }).ToList();
    }

    public async Task<(bool Success, string? Error, int? Id)> CreateAsync(
        int permitId, string energyType, string location, string? normalState, string? requiredIsolationState,
        string? isolationMethod, int? equipmentId, int? assetId, int? functionalLocationId)
    {
        if (!await _context.Permits.AnyAsync(x => x.PermitId == permitId && x.IsActive))
            return (false, "Permit پیدا نشد.", null);

        if (!ValidEnergyTypes.Contains(energyType, StringComparer.Ordinal))
            return (false, "نوع انرژی نامعتبره.", null);

        location = location?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(location))
            return (false, "محل نقطه‌ی ایزوله‌سازی الزامی است.", null);

        var point = new IsolationPoint
        {
            PermitId = permitId,
            EnergyType = energyType,
            Location = location,
            NormalState = normalState,
            RequiredIsolationState = requiredIsolationState,
            IsolationMethod = isolationMethod,
            EquipmentId = equipmentId,
            AssetId = assetId,
            FunctionalLocationId = functionalLocationId,
            Status = "Requested",
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.IsolationPoints.AddAsync(point);
        await _context.SaveChangesAsync();

        return (true, null, point.IsolationPointId);
    }

    public async Task<(bool Success, string? Error)> AdvanceStatusAsync(
        int isolationPointId, string nextStatus, string? reason, int? actorEmployeeId,
        string? lockNumber = null, string? tagNumber = null)
    {
        var point = await _context.IsolationPoints.FirstOrDefaultAsync(x => x.IsolationPointId == isolationPointId);
        if (point == null) return (false, "نقطه‌ی ایزوله‌سازی پیدا نشد.");

        var fromStatus = point.Status;

        if (!LegalTransitions.TryGetValue(fromStatus, out var allowed) || !allowed.Contains(nextStatus))
            return (false, $"گذار از وضعیت «{fromStatus}» به «{nextStatus}» مجاز نیست.");

        if (nextStatus == "Isolated")
        {
            point.IsolatedByEmployeeId = actorEmployeeId;
            point.IsolatedDate = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(lockNumber)) point.LockNumber = lockNumber;
            if (!string.IsNullOrWhiteSpace(tagNumber)) point.TagNumber = tagNumber;
        }

        if (nextStatus == "Verified")
        {
            point.VerifiedByEmployeeId = actorEmployeeId;
            point.VerifiedDate = DateTime.Now;
        }

        if (nextStatus == "Deisolated")
        {
            point.DeisolatedByEmployeeId = actorEmployeeId;
            point.DeisolatedDate = DateTime.Now;
        }

        point.Status = nextStatus;

        await _context.IsolationStatusHistories.AddAsync(new IsolationStatusHistory
        {
            IsolationPointId = point.IsolationPointId,
            FromStatus = fromStatus,
            ToStatus = nextStatus,
            ChangedById = actorEmployeeId,
            ChangedDate = DateTime.Now,
            Reason = reason
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }
}
