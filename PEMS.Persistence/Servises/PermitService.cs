using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Seed;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق CMMS-10a Execution Baseline: Core Permit Lifecycle. Approval Matrix واقعی (چه کسی
/// دقیقاً باید تایید کند) موضوع CMMS-10b است - این‌جا فقط گذار وضعیت خطی + کنترل‌شده پیاده
/// می‌شود، دقیقاً همون الگوی WorkRequestService (CMMS-03).
/// </summary>
public class PermitService : IPermitService
{
    private readonly PEMSDbContext _context;
    private readonly INumberingService _numberingService;
    private readonly IFunctionalLocationService _functionalLocationService;
    private readonly IWorkflowRuntime _runtime;

    public static readonly string[] ValidPermitTypes =
    {
        "Cold Work", "Hot Work", "Confined Space", "Electrical", "Excavation",
        "Working at Height", "Radiography", "Line Breaking", "Other"
    };

    // طبق بند ۱۱ سند: Draft→...→Closed خطی، به‌علاوه گذارهای استثنایی Cancelled/Rejected/Revoked.
    private static readonly Dictionary<string, string[]> LegalTransitions = new()
    {
        ["Draft"] = new[] { "Submitted", "Cancelled" },
        ["Submitted"] = new[] { "UnderReview", "Cancelled" },
        // "Draft" طبق CMMS-10b: مسیر Return/Rework - وقتی یک بازبین در زنجیره‌ی تایید
        // Permit را برای اصلاح برمی‌گرداند، نه رد قطعی می‌کند.
        ["UnderReview"] = new[] { "Approved", "Rejected", "Draft" },
        ["Approved"] = new[] { "Issued", "Revoked" },
        ["Issued"] = new[] { "Active", "Revoked" },
        ["Active"] = new[] { "Suspended", "Closed", "Revoked" },
        ["Suspended"] = new[] { "Active" },
    };

    public PermitService(PEMSDbContext context, INumberingService numberingService,
        IFunctionalLocationService functionalLocationService, IWorkflowRuntime runtime)
    {
        _context = context;
        _numberingService = numberingService;
        _functionalLocationService = functionalLocationService;
        _runtime = runtime;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): این متد کامل (بدون
    // Skip/Take) عمداً باقی مانده - مسیر pendingMyApproval در PermitController باید ابتدا
    // کل مجموعه‌ی فیلترشده‌ی-با-Status را بگیرد، سپس با IWorkflowRuntime (که ستونی از DB
    // نیست) فیلتر کند، و بعد صفحه‌بندی را روی نتیجه‌ی فیلترشده انجام دهد - وگرنه صفحه‌بندی
    // قبل از فیلتر واقعی، دقیقاً همون باگ MaterialItem.lowStockOnly را تکرار می‌کرد. مسیر
    // معمولی (بدون pendingMyApproval) باید از GetPagedListAsync زیر استفاده کند.
    public async Task<List<PermitListDto>> GetListAsync(string? status = null)
    {
        return await BuildListQuery(status)
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.PermitId)
            .Select(PermitListProjection)
            .ToListAsync();
    }

    public async Task<PagedResult<PermitListDto>> GetPagedListAsync(string? status, int page, int pageSize)
    {
        var query = BuildListQuery(status);
        var totalCount = await query.CountAsync();

        page = page < 1 ? 1 : page;
        pageSize = PEMS.Application.Common.DataGridPageSize.Normalize(pageSize);

        var items = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.PermitId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(PermitListProjection)
            .ToListAsync();

        return new PagedResult<PermitListDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    private IQueryable<Permit> BuildListQuery(string? status)
    {
        var query = _context.Permits.Where(x => x.IsActive).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return query;
    }

    private static readonly System.Linq.Expressions.Expression<Func<Permit, PermitListDto>> PermitListProjection = x => new PermitListDto
    {
        PermitId = x.PermitId,
        PermitNumber = x.PermitNumber,
        PermitType = x.PermitType,
        Title = x.Title,
        Status = x.Status,
        Priority = x.Priority,
        EquipmentTagNumber = x.Equipment != null ? x.Equipment.TagNumber : null,
        WorkOrderNumber = x.WorkOrder != null ? x.WorkOrder.WorkOrderNumber : null,
        IssuerName = x.PermitIssuerEmployee != null ? x.PermitIssuerEmployee.FirstName + " " + x.PermitIssuerEmployee.LastName : null,
        ReceiverName = x.PermitReceiverEmployee != null ? x.PermitReceiverEmployee.FirstName + " " + x.PermitReceiverEmployee.LastName : null,
        ValidFrom = x.ValidFrom,
        ValidTo = x.ValidTo,
        CreatedDate = x.CreatedDate
    };

    public async Task<PermitDetailDto?> GetDetailsAsync(int permitId)
    {
        var permit = await _context.Permits
            .Include(x => x.Equipment)
            .Include(x => x.WorkOrder)
            .Include(x => x.RequestedByEmployee)
            .Include(x => x.PermitIssuerEmployee)
            .Include(x => x.PermitReceiverEmployee).ThenInclude(e => e!.VendorRef)
            .FirstOrDefaultAsync(x => x.PermitId == permitId);

        if (permit == null) return null;

        var history = await _context.PermitStatusHistories
            .Include(x => x.ChangedBy)
            .Where(x => x.PermitId == permitId)
            .OrderByDescending(x => x.ChangedDate)
            .Select(x => new PermitStatusHistoryDto
            {
                FromStatus = x.FromStatus,
                ToStatus = x.ToStatus,
                ChangedByName = x.ChangedBy != null ? x.ChangedBy.FirstName + " " + x.ChangedBy.LastName : null,
                ChangedDate = x.ChangedDate,
                Reason = x.Reason
            })
            .ToListAsync();

        return new PermitDetailDto
        {
            PermitId = permit.PermitId,
            PermitNumber = permit.PermitNumber,
            PermitType = permit.PermitType,
            Title = permit.Title,
            Description = permit.Description,
            EquipmentId = permit.EquipmentId,
            EquipmentTagNumber = permit.Equipment?.TagNumber,
            FunctionalLocationId = permit.FunctionalLocationId,
            FunctionalLocationBreadcrumb = await _functionalLocationService.GetBreadcrumbAsync(permit.FunctionalLocationId),
            WorkOrderId = permit.WorkOrderId,
            WorkOrderNumber = permit.WorkOrder?.WorkOrderNumber,
            RequestedByName = permit.RequestedByEmployee != null ? $"{permit.RequestedByEmployee.FirstName} {permit.RequestedByEmployee.LastName}" : "",
            IssuerName = permit.PermitIssuerEmployee != null ? $"{permit.PermitIssuerEmployee.FirstName} {permit.PermitIssuerEmployee.LastName}" : null,
            ReceiverName = permit.PermitReceiverEmployee != null ? $"{permit.PermitReceiverEmployee.FirstName} {permit.PermitReceiverEmployee.LastName}" : null,
            ReceiverPersonnelType = permit.PermitReceiverEmployee?.PersonnelType,
            ReceiverVendorName = permit.PermitReceiverEmployee?.VendorRef?.Name,
            Status = permit.Status,
            Priority = permit.Priority,
            ValidFrom = permit.ValidFrom,
            ValidTo = permit.ValidTo,
            ClosedDate = permit.ClosedDate,
            CancellationReason = permit.CancellationReason,
            RevocationReason = permit.RevocationReason,
            CreatedDate = permit.CreatedDate,
            StatusHistory = history
        };
    }

    public async Task<(bool Success, string? Error, int? Id)> CreateAsync(
        string permitType, string title, string? description, int requestedByEmployeeId,
        int? equipmentId, int? functionalLocationId, int? workOrderId, int? permitReceiverEmployeeId,
        string priority, DateTime? validFrom, DateTime? validTo)
    {
        title = title?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(title)) return (false, "عنوان Permit الزامی است.", null);

        if (!ValidPermitTypes.Contains(permitType, StringComparer.Ordinal))
            return (false, "نوع Permit نامعتبره.", null);

        if (!await _context.Employees.AnyAsync(x => x.EmployeeId == requestedByEmployeeId && x.IsActive))
            return (false, "کارمند درخواست‌دهنده نامعتبره.", null);

        if (validFrom.HasValue && validTo.HasValue && validTo.Value < validFrom.Value)
            return (false, "تاریخ پایان اعتبار نمی‌تواند قبل از تاریخ شروع باشد.", null);

        var permit = new Permit
        {
            PermitNumber = await _numberingService.GetNextNumberAsync("Permit"),
            PermitType = permitType,
            Title = title,
            Description = description,
            RequestedByEmployeeId = requestedByEmployeeId,
            EquipmentId = equipmentId,
            FunctionalLocationId = functionalLocationId,
            WorkOrderId = workOrderId,
            PermitReceiverEmployeeId = permitReceiverEmployeeId,
            Priority = string.IsNullOrWhiteSpace(priority) ? "Medium" : priority,
            ValidFrom = validFrom,
            ValidTo = validTo,
            Status = "Draft",
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.Permits.AddAsync(permit);
        await _context.SaveChangesAsync();

        return (true, null, permit.PermitId);
    }

    public async Task<(bool Success, string? Error)> AdvanceStatusAsync(
        int permitId, string nextStatus, string? reason, int? changedByEmployeeId)
    {
        var permit = await _context.Permits.FirstOrDefaultAsync(x => x.PermitId == permitId);
        if (permit == null) return (false, "Permit پیدا نشد.");

        var fromStatus = permit.Status;

        if (!LegalTransitions.TryGetValue(fromStatus, out var allowed) || !allowed.Contains(nextStatus))
            return (false, $"گذار از وضعیت «{fromStatus}» به «{nextStatus}» مجاز نیست.");

        if (nextStatus == "Issued")
            permit.PermitIssuerEmployeeId = changedByEmployeeId;

        if (nextStatus == "Closed")
            permit.ClosedDate = DateTime.Now;

        if (nextStatus == "Cancelled")
            permit.CancellationReason = reason;

        if (nextStatus == "Revoked")
            permit.RevocationReason = reason;

        permit.Status = nextStatus;

        await _context.PermitStatusHistories.AddAsync(new PermitStatusHistory
        {
            PermitId = permit.PermitId,
            FromStatus = fromStatus,
            ToStatus = nextStatus,
            ChangedById = changedByEmployeeId,
            ChangedDate = DateTime.Now,
            Reason = reason
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    // ==============================================================================
    // طبق CMMS-10b: شروع زنجیره‌ی تایید واقعی از طریق IWorkflowRuntime موجود - همون الگوی
    // WorkOrderReviewService.RequestDisciplineReviewAsync، نه یک موتور موازی.
    // ==============================================================================
    public async Task<(bool Success, string? Error)> SubmitForApprovalAsync(int permitId, int? actorEmployeeId)
    {
        var permit = await _context.Permits.FirstOrDefaultAsync(x => x.PermitId == permitId);
        if (permit == null) return (false, "Permit پیدا نشد.");

        if (permit.Status != "Submitted")
            return (false, $"فقط از وضعیت «Submitted» می‌توان زنجیره‌ی تایید را شروع کرد (وضعیت فعلی: «{permit.Status}»).");

        var (success, error) = await AdvanceStatusAsync(permitId, "UnderReview", null, actorEmployeeId);
        if (!success) return (success, error);

        await _runtime.StartInstanceAsync(
            PermitApprovalWorkflowSeeder.Module, PermitApprovalWorkflowSeeder.BusinessObjectType, permitId,
            PermitApprovalWorkflowSeeder.Category, null, actorEmployeeId);

        return (true, null);
    }

    // ==============================================================================
    // طبق CMMS-10b بند ۸: برخلاف الگوی ساده‌ی QCController (که رشته‌ی decision را مستقیم به
    // Status کسب‌وکاری نگاشت می‌کرد - درست فقط چون NCR یک‌مرحله‌ای است)، این‌جا بعد از اجرای
    // اکشن، وضعیت واقعیِ کل Instance دوباره خوانده می‌شود: فقط وقتی زنجیره‌ی چندمرحله‌ای واقعاً
    // «Completed» شده Permit به Approved می‌رود؛ اگر هنوز «Running» است (مرحله‌ی میانی تایید
    // شد)، Permit در UnderReview می‌ماند.
    // ==============================================================================
    public async Task<(bool Success, string? Error)> RespondApprovalAsync(
        int permitId, string decision, string? comment, int? actorEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress, string? userAgent)
    {
        var permit = await _context.Permits.FirstOrDefaultAsync(x => x.PermitId == permitId);
        if (permit == null) return (false, "Permit پیدا نشد.");

        if (permit.Status != "UnderReview")
            return (false, $"این Permit در وضعیت «UnderReview» نیست (وضعیت فعلی: «{permit.Status}»).");

        var instance = await _runtime.GetInstanceAsync(PermitApprovalWorkflowSeeder.BusinessObjectType, permitId);
        if (instance == null || instance.Status != "Running")
            return (false, "زنجیره‌ی تایید فعالی برای این Permit یافت نشد.");

        try
        {
            await _runtime.ExecuteActionAsync(
                instance.WorkflowInstanceId, decision, actorEmployeeId, actorRoles, comment, ipAddress, userAgent);
        }
        catch (UnauthorizedAccessException ex)
        {
            return (false, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message);
        }

        var updatedInstance = await _runtime.GetInstanceByIdAsync(instance.WorkflowInstanceId);

        if (updatedInstance?.Status == "Completed")
            return await AdvanceStatusAsync(permitId, "Approved", "زنجیره‌ی تایید کامل شد.", actorEmployeeId);

        if (updatedInstance?.Status == "Rejected")
            return await AdvanceStatusAsync(permitId, "Rejected", comment, actorEmployeeId);

        // زنجیره هنوز در جریان است (مرحله‌ی میانی تایید شد) - Permit در UnderReview می‌ماند
        return (true, null);
    }

    // ==============================================================================
    // طبق CMMS-10b بند ۱۱: موتور گردش‌کار هیچ وضعیت «Return» مجزایی ندارد - یک گذار پایانی
    // با ActionType غیر از "Reject" همیشه Instance.Status="Completed" ثبت می‌شود (که با تایید
    // واقعی غیرقابل‌تفکیک می‌شود). به‌جای تغییر موتور مشترک (که ۶ ماژول دیگر هم ازش استفاده
    // می‌کنند)، این‌جا از همون Action "Reject" کاملاً مشخص موتور استفاده می‌شود (زنجیره با
    // نتیجه‌ی Rejected بسته می‌شود)، ولی در سطح Permit، به‌جای انتقال به Rejected نهایی، Permit
    // به Draft (قابل ویرایش/بازبینی مجدد) برمی‌گردد - دقیقاً طبق تمایز معنایی خواسته‌شده.
    // ==============================================================================
    public async Task<(bool Success, string? Error)> ReturnForReworkAsync(
        int permitId, string? comment, int? actorEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress, string? userAgent)
    {
        var permit = await _context.Permits.FirstOrDefaultAsync(x => x.PermitId == permitId);
        if (permit == null) return (false, "Permit پیدا نشد.");

        if (permit.Status != "UnderReview")
            return (false, $"این Permit در وضعیت «UnderReview» نیست (وضعیت فعلی: «{permit.Status}»).");

        var instance = await _runtime.GetInstanceAsync(PermitApprovalWorkflowSeeder.BusinessObjectType, permitId);
        if (instance == null || instance.Status != "Running")
            return (false, "زنجیره‌ی تایید فعالی برای این Permit یافت نشد.");

        try
        {
            await _runtime.ExecuteActionAsync(
                instance.WorkflowInstanceId, "Reject", actorEmployeeId, actorRoles, comment, ipAddress, userAgent);
        }
        catch (UnauthorizedAccessException ex)
        {
            return (false, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message);
        }

        return await AdvanceStatusAsync(permitId, "Draft",
            comment != null ? $"بازگشت برای اصلاح: {comment}" : "بازگشت برای اصلاح.", actorEmployeeId);
    }

    public async Task<bool> HasPendingApprovalAsync(int permitId)
    {
        var instance = await _runtime.GetInstanceAsync(PermitApprovalWorkflowSeeder.BusinessObjectType, permitId);
        return instance != null && instance.Status == "Running";
    }
}
