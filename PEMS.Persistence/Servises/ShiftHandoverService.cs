using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق CMMS-10e: ShiftHandover والد Permit نیست - چندین Permit/WorkOrder را هم‌زمان در بر
/// می‌گیرد، پس ماژول مستقل خودش را دارد (نه یک کارت روی Permit/Details). PrepareAsync فقط یک‌بار
/// Snapshot می‌گیرد (ShiftHandoverItem) - هرگز دوباره تولید نمی‌شود. Acknowledge/Complete علاوه
/// بر گیت درشت RoleModuleAction، با شناسه‌ی دقیق کارمند (Incoming/Outgoing) هم بررسی می‌شوند -
/// نه فقط نقش، طبق تست امنیتی صریح دستورالعمل ("Wrong role acknowledgement").
/// </summary>
public class ShiftHandoverService : IShiftHandoverService
{
    private readonly PEMSDbContext _context;

    private static readonly Dictionary<string, string[]> LegalTransitions = new()
    {
        ["Draft"] = new[] { "Prepared" },
        ["Prepared"] = new[] { "Acknowledged" },
        ["Acknowledged"] = new[] { "Completed" },
    };

    public ShiftHandoverService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<ShiftHandoverListDto>> GetAllAsync(string? status)
    {
        var query = _context.ShiftHandovers
            .Include(x => x.OutgoingEmployee)
            .Include(x => x.IncomingEmployee)
            .Include(x => x.Items)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        var handovers = await query.OrderByDescending(x => x.HandoverDate).ToListAsync();

        return handovers.Select(h => new ShiftHandoverListDto
        {
            ShiftHandoverId = h.ShiftHandoverId,
            OutgoingName = FullName(h.OutgoingEmployee),
            IncomingName = FullName(h.IncomingEmployee),
            ShiftLabel = h.ShiftLabel,
            HandoverDate = h.HandoverDate,
            Status = h.Status,
            PreparedDate = h.PreparedDate,
            AcknowledgedDate = h.AcknowledgedDate,
            CompletedDate = h.CompletedDate,
            ItemCount = h.Items.Count
        }).ToList();
    }

    public async Task<ShiftHandoverDetailDto?> GetDetailsAsync(int id)
    {
        var h = await _context.ShiftHandovers
            .Include(x => x.OutgoingEmployee)
            .Include(x => x.IncomingEmployee)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.ShiftHandoverId == id);
        if (h == null) return null;

        var histories = await _context.ShiftHandoverStatusHistories
            .Include(x => x.ChangedBy)
            .Where(x => x.ShiftHandoverId == id)
            .OrderByDescending(x => x.ChangedDate)
            .ToListAsync();

        return new ShiftHandoverDetailDto
        {
            ShiftHandoverId = h.ShiftHandoverId,
            OutgoingEmployeeId = h.OutgoingEmployeeId,
            OutgoingName = FullName(h.OutgoingEmployee),
            IncomingEmployeeId = h.IncomingEmployeeId,
            IncomingName = FullName(h.IncomingEmployee),
            ShiftLabel = h.ShiftLabel,
            HandoverDate = h.HandoverDate,
            Notes = h.Notes,
            Status = h.Status,
            PreparedDate = h.PreparedDate,
            AcknowledgedDate = h.AcknowledgedDate,
            CompletedDate = h.CompletedDate,
            Items = h.Items.Select(i => new ShiftHandoverItemDto
            {
                ItemType = i.ItemType,
                ReferenceType = i.ReferenceType,
                ReferenceId = i.ReferenceId,
                Description = i.Description
            }).ToList(),
            StatusHistory = histories.Select(x => new ShiftHandoverStatusHistoryDto
            {
                FromStatus = x.FromStatus,
                ToStatus = x.ToStatus,
                ChangedByName = FullName(x.ChangedBy),
                ChangedDate = x.ChangedDate,
                Reason = x.Reason
            }).ToList()
        };
    }

    public async Task<(bool Success, string? Error, int? Id)> CreateAsync(
        int outgoingEmployeeId, int incomingEmployeeId, string? shiftLabel, DateTime handoverDate, string? notes)
    {
        if (outgoingEmployeeId == incomingEmployeeId)
            return (false, "کارمند خروجی و ورودی باید متفاوت باشند.", null);

        if (!await _context.Employees.AnyAsync(x => x.EmployeeId == outgoingEmployeeId && x.IsActive))
            return (false, "کارمند خروجی پیدا نشد.", null);
        if (!await _context.Employees.AnyAsync(x => x.EmployeeId == incomingEmployeeId && x.IsActive))
            return (false, "کارمند ورودی پیدا نشد.", null);

        var handover = new ShiftHandover
        {
            OutgoingEmployeeId = outgoingEmployeeId,
            IncomingEmployeeId = incomingEmployeeId,
            ShiftLabel = shiftLabel,
            HandoverDate = handoverDate,
            Notes = notes,
            Status = "Draft",
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.ShiftHandovers.AddAsync(handover);
        await _context.SaveChangesAsync();

        await _context.ShiftHandoverStatusHistories.AddAsync(new ShiftHandoverStatusHistory
        {
            ShiftHandoverId = handover.ShiftHandoverId,
            FromStatus = "",
            ToStatus = "Draft",
            ChangedById = outgoingEmployeeId,
            ChangedDate = DateTime.Now,
            Reason = "ایجاد اولیه"
        });
        await _context.SaveChangesAsync();

        return (true, null, handover.ShiftHandoverId);
    }

    public async Task<(bool Success, string? Error)> PrepareAsync(int id, int? actorEmployeeId)
    {
        var handover = await _context.ShiftHandovers.FirstOrDefaultAsync(x => x.ShiftHandoverId == id);
        if (handover == null) return (false, "تحویل شیفت پیدا نشد.");

        if (actorEmployeeId != handover.OutgoingEmployeeId)
            return (false, "فقط کارمند خروجی می‌تواند این تحویل شیفت را آماده کند.");

        var fromStatus = handover.Status;
        if (!LegalTransitions.TryGetValue(fromStatus, out var allowed) || !allowed.Contains("Prepared"))
            return (false, $"گذار از وضعیت «{fromStatus}» به «Prepared» مجاز نیست.");

        var items = await BuildSnapshotItemsAsync(handover.Notes);
        foreach (var item in items)
        {
            item.ShiftHandoverId = handover.ShiftHandoverId;
            await _context.ShiftHandoverItems.AddAsync(item);
        }

        handover.Status = "Prepared";
        handover.PreparedDate = DateTime.Now;

        await _context.ShiftHandoverStatusHistories.AddAsync(new ShiftHandoverStatusHistory
        {
            ShiftHandoverId = handover.ShiftHandoverId,
            FromStatus = fromStatus,
            ToStatus = "Prepared",
            ChangedById = actorEmployeeId,
            ChangedDate = DateTime.Now,
            Reason = "آماده‌سازی و ثبت وضعیت لحظه‌ای عملیات"
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AcknowledgeAsync(int id, int? actorEmployeeId)
    {
        var handover = await _context.ShiftHandovers.FirstOrDefaultAsync(x => x.ShiftHandoverId == id);
        if (handover == null) return (false, "تحویل شیفت پیدا نشد.");

        if (actorEmployeeId != handover.IncomingEmployeeId)
            return (false, "فقط کارمند ورودی می‌تواند این تحویل شیفت را تایید کند.");

        var fromStatus = handover.Status;
        if (!LegalTransitions.TryGetValue(fromStatus, out var allowed) || !allowed.Contains("Acknowledged"))
            return (false, $"گذار از وضعیت «{fromStatus}» به «Acknowledged» مجاز نیست.");

        handover.Status = "Acknowledged";
        handover.AcknowledgedDate = DateTime.Now;

        await _context.ShiftHandoverStatusHistories.AddAsync(new ShiftHandoverStatusHistory
        {
            ShiftHandoverId = handover.ShiftHandoverId,
            FromStatus = fromStatus,
            ToStatus = "Acknowledged",
            ChangedById = actorEmployeeId,
            ChangedDate = DateTime.Now,
            Reason = "تایید دریافت توسط کارمند ورودی"
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CompleteAsync(int id, int? actorEmployeeId)
    {
        var handover = await _context.ShiftHandovers.FirstOrDefaultAsync(x => x.ShiftHandoverId == id);
        if (handover == null) return (false, "تحویل شیفت پیدا نشد.");

        if (actorEmployeeId != handover.OutgoingEmployeeId)
            return (false, "فقط کارمند خروجی می‌تواند این تحویل شیفت را نهایی کند.");

        var fromStatus = handover.Status;
        if (!LegalTransitions.TryGetValue(fromStatus, out var allowed) || !allowed.Contains("Completed"))
            return (false, $"گذار از وضعیت «{fromStatus}» به «Completed» مجاز نیست.");

        handover.Status = "Completed";
        handover.CompletedDate = DateTime.Now;

        await _context.ShiftHandoverStatusHistories.AddAsync(new ShiftHandoverStatusHistory
        {
            ShiftHandoverId = handover.ShiftHandoverId,
            FromStatus = fromStatus,
            ToStatus = "Completed",
            ChangedById = actorEmployeeId,
            ChangedDate = DateTime.Now,
            Reason = "بستن نهایی توسط کارمند خروجی"
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    private async Task<List<ShiftHandoverItem>> BuildSnapshotItemsAsync(string? notes)
    {
        var items = new List<ShiftHandoverItem>();
        var now = DateTime.Now;

        var activePermits = await _context.Permits.Where(x => x.IsActive && (x.Status == "Issued" || x.Status == "Active")).ToListAsync();
        items.AddRange(activePermits.Select(p => new ShiftHandoverItem
        {
            ItemType = "ActivePermit", ReferenceType = "Permit", ReferenceId = p.PermitId,
            Description = $"{p.PermitNumber} — {p.PermitType} — {p.Title} — Status: {p.Status}", CreatedDate = now
        }));

        var suspendedPermits = await _context.Permits.Where(x => x.IsActive && x.Status == "Suspended").ToListAsync();
        items.AddRange(suspendedPermits.Select(p => new ShiftHandoverItem
        {
            ItemType = "SuspendedPermit", ReferenceType = "Permit", ReferenceId = p.PermitId,
            Description = $"{p.PermitNumber} — {p.PermitType} — {p.Title} — Suspended", CreatedDate = now
        }));

        var openWorkOrders = await _context.WorkOrders.Where(x => x.IsActive && x.Status != "Completed" && x.Status != "Closed").ToListAsync();
        items.AddRange(openWorkOrders.Select(w => new ShiftHandoverItem
        {
            ItemType = "OpenWorkOrder", ReferenceType = "WorkOrder", ReferenceId = w.WorkOrderId,
            Description = $"{w.WorkOrderNumber} — {w.WorkOrderType} — Status: {w.Status}", CreatedDate = now
        }));

        var activeIsolations = await _context.IsolationPoints
            .Include(x => x.Permit)
            .Where(x => x.IsActive && (x.Status == "Isolated" || x.Status == "Verified"))
            .ToListAsync();
        items.AddRange(activeIsolations.Select(i => new ShiftHandoverItem
        {
            ItemType = "ActiveIsolation", ReferenceType = "IsolationPoint", ReferenceId = i.IsolationPointId,
            Description = $"{i.Permit?.PermitNumber} / {i.EnergyType} — {i.Location} — Status: {i.Status}", CreatedDate = now
        }));

        var validGasTests = await _context.GasTests
            .Include(x => x.Permit)
            .Where(x => x.IsActive && x.Status == "Passed" && x.ValidUntil != null && x.ValidUntil >= now)
            .ToListAsync();
        items.AddRange(validGasTests.Select(g => new ShiftHandoverItem
        {
            ItemType = "ValidGasTest", ReferenceType = "GasTest", ReferenceId = g.GasTestId,
            Description = $"{g.Permit?.PermitNumber} — {g.Location} — Valid Until {g.ValidUntil:yyyy-MM-dd HH:mm}", CreatedDate = now
        }));

        var openApprovals = await _context.Permits.Where(x => x.IsActive && x.Status == "UnderReview").ToListAsync();
        items.AddRange(openApprovals.Select(p => new ShiftHandoverItem
        {
            ItemType = "OpenApproval", ReferenceType = "Permit", ReferenceId = p.PermitId,
            Description = $"{p.PermitNumber} — در انتظار تایید", CreatedDate = now
        }));

        var outstandingHazards = await _context.SimopsConflicts
            .Include(x => x.PermitA).Include(x => x.PermitB)
            .Where(x => x.IsActive && (x.Status == "Potential" || x.Status == "Confirmed"))
            .ToListAsync();
        items.AddRange(outstandingHazards.Select(c => new ShiftHandoverItem
        {
            ItemType = "OutstandingHazard", ReferenceType = "SimopsConflict", ReferenceId = c.SimopsConflictId,
            Description = $"{c.PermitA?.PermitNumber} vs {c.PermitB?.PermitNumber} — Severity {c.Severity} — {c.Status}", CreatedDate = now
        }));

        if (!string.IsNullOrWhiteSpace(notes))
        {
            var lines = notes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            items.AddRange(lines.Select(line => new ShiftHandoverItem
            {
                ItemType = "OutstandingAction", ReferenceType = null, ReferenceId = null,
                Description = line, CreatedDate = now
            }));
        }

        return items;
    }

    private static string? FullName(Employee? e) => e != null ? $"{e.FirstName} {e.LastName}" : null;
}
