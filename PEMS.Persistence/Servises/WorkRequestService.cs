using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق CMMS Production Baseline v1 بند P0-05: پیاده‌سازی مرجع «اعتبارسنجی گذار وضعیت در
/// Service، نه فقط Controller» - همان الگویی که در فاز بعدی به WorkOrderLifecycleService هم
/// اضافه می‌شود.
/// </summary>
public class WorkRequestService : IWorkRequestService
{
    private readonly PEMSDbContext _context;
    private readonly INumberingService _numberingService;
    private readonly IFunctionalLocationService _functionalLocationService;

    private static readonly Dictionary<string, string[]> LegalTransitions = new()
    {
        ["Draft"] = new[] { "Submitted", "Cancelled" },
        ["Submitted"] = new[] { "UnderReview", "Cancelled" },
        ["UnderReview"] = new[] { "Approved", "Rejected", "Cancelled" },
        ["Approved"] = new[] { "Planned", "Rejected", "Cancelled" },
        ["Planned"] = new[] { "Converted", "Cancelled" },
    };

    public WorkRequestService(PEMSDbContext context, INumberingService numberingService, IFunctionalLocationService functionalLocationService)
    {
        _context = context;
        _numberingService = numberingService;
        _functionalLocationService = functionalLocationService;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): قبلاً کل جدول فیلترشده
    // با ToListAsync (بدون Skip/Take) بارگذاری می‌شد. دو Include قبلی هم غیرضروری بودند - چون
    // Select پایین‌تر مستقیم فیلد اسکالر می‌گیرد، نه خودِ Entity ناوبری‌شده را.
    public async Task<PagedResult<WorkRequestListDto>> GetListAsync(int page = 1, int pageSize = 50, string? status = null)
    {
        var query = _context.WorkRequests.Where(x => x.IsActive).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

        var totalCount = await query.CountAsync();

        page = page < 1 ? 1 : page;
        pageSize = DataGridPageSize.Normalize(pageSize);

        var items = await query
            .OrderByDescending(x => x.RequestedDate)
            .ThenByDescending(x => x.WorkRequestId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new WorkRequestListDto
            {
                WorkRequestId = x.WorkRequestId,
                RequestNumber = x.RequestNumber,
                Title = x.Title,
                Status = x.Status,
                Priority = x.Priority,
                RequestedByName = x.RequestedBy != null ? x.RequestedBy.FirstName + " " + x.RequestedBy.LastName : "",
                RequestedDate = x.RequestedDate,
                EquipmentTagNumber = x.Equipment != null ? x.Equipment.TagNumber : null
            })
            .ToListAsync();

        return new PagedResult<WorkRequestListDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<WorkRequestDetailDto?> GetDetailsAsync(int workRequestId)
    {
        var wr = await _context.WorkRequests
            .Include(x => x.RequestedBy)
            .Include(x => x.ReviewedBy)
            .Include(x => x.ApprovedBy)
            .Include(x => x.Equipment)
            .Include(x => x.Asset)
            .Include(x => x.SuggestedDiscipline)
            .Include(x => x.LinkedWorkOrder)
            .FirstOrDefaultAsync(x => x.WorkRequestId == workRequestId);

        if (wr == null) return null;

        var history = await _context.WorkRequestStatusHistories
            .Include(x => x.ChangedBy)
            .Where(x => x.WorkRequestId == workRequestId)
            .OrderByDescending(x => x.ChangedDate)
            .Select(x => new WorkRequestStatusHistoryDto
            {
                FromStatus = x.FromStatus,
                ToStatus = x.ToStatus,
                ChangedByName = x.ChangedBy != null ? x.ChangedBy.FirstName + " " + x.ChangedBy.LastName : null,
                ChangedDate = x.ChangedDate,
                Reason = x.Reason
            })
            .ToListAsync();

        string? criticalityClass = null;
        if (wr.EquipmentId.HasValue)
        {
            criticalityClass = await _context.EquipmentCriticalities
                .Where(x => x.EquipmentId == wr.EquipmentId.Value && x.IsActive)
                .Select(x => x.CriticalityClass)
                .FirstOrDefaultAsync();
        }

        return new WorkRequestDetailDto
        {
            WorkRequestId = wr.WorkRequestId,
            RequestNumber = wr.RequestNumber,
            Title = wr.Title,
            Description = wr.Description,
            RequestedByName = wr.RequestedBy != null ? $"{wr.RequestedBy.FirstName} {wr.RequestedBy.LastName}" : "",
            RequestedDate = wr.RequestedDate,
            EquipmentId = wr.EquipmentId,
            EquipmentTagNumber = wr.Equipment?.TagNumber,
            AssetId = wr.AssetId,
            AssetNumber = wr.Asset?.AssetNumber,
            FunctionalLocationId = wr.FunctionalLocationId,
            FunctionalLocationBreadcrumb = await _functionalLocationService.GetBreadcrumbAsync(wr.FunctionalLocationId),
            ReportedCondition = wr.ReportedCondition,
            Priority = wr.Priority,
            EquipmentCriticalityClass = criticalityClass,
            SuggestedDisciplineId = wr.SuggestedDisciplineId,
            SuggestedDisciplineName = wr.SuggestedDiscipline?.Name,
            Status = wr.Status,
            ReviewedByName = wr.ReviewedBy != null ? $"{wr.ReviewedBy.FirstName} {wr.ReviewedBy.LastName}" : null,
            ReviewedDate = wr.ReviewedDate,
            RejectionReason = wr.RejectionReason,
            ApprovedByName = wr.ApprovedBy != null ? $"{wr.ApprovedBy.FirstName} {wr.ApprovedBy.LastName}" : null,
            ApprovedDate = wr.ApprovedDate,
            LinkedWorkOrderId = wr.LinkedWorkOrderId,
            LinkedWorkOrderNumber = wr.LinkedWorkOrder?.WorkOrderNumber,
            StatusHistory = history
        };
    }

    public async Task<(bool Success, string? Error, int? Id)> CreateAsync(
        string title, string? description, int requestedById, int? equipmentId, int? assetId,
        int? functionalLocationId, string? reportedCondition, string priority, int? suggestedDisciplineId)
    {
        title = title?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(title)) return (false, "عنوان درخواست الزامی است.", null);

        if (!await _context.Employees.AnyAsync(x => x.EmployeeId == requestedById && x.IsActive))
            return (false, "کارمند درخواست‌دهنده نامعتبره.", null);

        var workRequest = new WorkRequest
        {
            RequestNumber = await _numberingService.GetNextNumberAsync("WorkRequest"),
            Title = title,
            Description = description,
            RequestedById = requestedById,
            RequestedDate = DateTime.Now,
            EquipmentId = equipmentId,
            AssetId = assetId,
            FunctionalLocationId = functionalLocationId,
            ReportedCondition = reportedCondition,
            Priority = string.IsNullOrWhiteSpace(priority) ? "Medium" : priority,
            SuggestedDisciplineId = suggestedDisciplineId,
            Status = "Draft",
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.WorkRequests.AddAsync(workRequest);
        await _context.SaveChangesAsync();

        return (true, null, workRequest.WorkRequestId);
    }

    public async Task<(bool Success, string? Error)> AdvanceStatusAsync(
        int workRequestId, string nextStatus, string? reason, int? changedByEmployeeId)
    {
        var wr = await _context.WorkRequests.FirstOrDefaultAsync(x => x.WorkRequestId == workRequestId);
        if (wr == null) return (false, "Work Request پیدا نشد.");

        if (!LegalTransitions.TryGetValue(wr.Status, out var allowed) || !allowed.Contains(nextStatus))
            return (false, $"گذار از وضعیت «{wr.Status}» به «{nextStatus}» مجاز نیست.");

        var fromStatus = wr.Status;

        if (nextStatus == "UnderReview")
        {
            wr.ReviewedById = changedByEmployeeId;
            wr.ReviewedDate = DateTime.Now;
        }

        if (nextStatus == "Approved")
        {
            wr.ApprovedById = changedByEmployeeId;
            wr.ApprovedDate = DateTime.Now;
        }

        if (nextStatus == "Rejected")
        {
            wr.RejectionReason = reason;
        }

        wr.Status = nextStatus;

        await _context.WorkRequestStatusHistories.AddAsync(new WorkRequestStatusHistory
        {
            WorkRequestId = wr.WorkRequestId,
            FromStatus = fromStatus,
            ToStatus = nextStatus,
            ChangedById = changedByEmployeeId,
            ChangedDate = DateTime.Now,
            Reason = reason
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error, int? WorkOrderId)> ConvertToWorkOrderAsync(
        int workRequestId, string workOrderType, string? currentUserName)
    {
        var wr = await _context.WorkRequests.Include(x => x.Equipment).FirstOrDefaultAsync(x => x.WorkRequestId == workRequestId);
        if (wr == null) return (false, "Work Request پیدا نشد.", null);

        if (wr.Status != "Planned")
            return (false, "فقط درخواست‌های در وضعیت Planned قابل تبدیل به WorkOrder هستند.", null);

        if (wr.LinkedWorkOrderId.HasValue)
            return (false, "این درخواست قبلاً به یک WorkOrder تبدیل شده.", null);

        if (!wr.EquipmentId.HasValue)
            return (false, "برای ساخت WorkOrder، این درخواست باید به یک تجهیز مشخص وصل باشد.", null);

        var workOrderNumber = await _numberingService.GetNextNumberAsync("WorkOrder");

        var workOrder = new WorkOrder
        {
            EquipmentId = wr.EquipmentId.Value,
            SourceWorkRequestId = wr.WorkRequestId,
            WorkOrderNumber = workOrderNumber,
            WorkOrderType = workOrderType,
            Priority = wr.Priority,
            Status = "Open",
            Description = wr.Title + (string.IsNullOrEmpty(wr.Description) ? "" : " - " + wr.Description),
            IsActive = true,
            CreatedBy = currentUserName,
            CreatedDate = DateTime.Now
        };

        var fromStatus = wr.Status;
        // از navigation property به جای FK خام استفاده شد چون workOrder هنوز Id واقعی ندارد -
        // EF Core موقع SaveChanges به‌صورت خودکار FK را از روی object reference پر می‌کند (fixup).
        wr.LinkedWorkOrder = workOrder;
        wr.Status = "Converted";
        wr.ModifiedBy = currentUserName;

        // هر دو تغییر (ساخت WorkOrder و به‌روزرسانی WorkRequest) باید یک‌جا و اتمیک ذخیره شوند
        // تا در صورت خطا بین این دو مرحله، WorkOrder یتیم بدون به‌روزرسانی WorkRequest باقی نماند.
        await _context.WorkOrders.AddAsync(workOrder);
        await _context.WorkRequestStatusHistories.AddAsync(new WorkRequestStatusHistory
        {
            WorkRequestId = wr.WorkRequestId,
            FromStatus = fromStatus,
            ToStatus = "Converted",
            Reason = $"تبدیل به {workOrderNumber}",
            ChangedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        return (true, null, workOrder.WorkOrderId);
    }
}
