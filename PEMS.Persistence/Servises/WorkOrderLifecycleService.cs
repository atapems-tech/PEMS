using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق PEMS_CMMS_ARCHITECTURE.md بخش ۸، ۱۰، ۱۲: منطق جابجایی وضعیت WorkOrder که قبلاً مستقیم
/// در Controller بود، حالا این‌جاست تا Controller نازک بماند (طبق CLAUDE.md).
/// </summary>
public class WorkOrderLifecycleService : IWorkOrderLifecycleService
{
    private readonly PEMSDbContext _context;

    // طبق CMMS Production Baseline v1 بند ۹.۳ («State Machine Rule» - Do not rely only on
    // Controller logic): قبلاً هیچ اعتبارسنجی‌ای این‌جا نبود؛ AdvanceStatusAsync هر nextStatus
    // ای رو بدون چک اعمال می‌کرد و تنها سد واقعی، GetNextStatus داخل Controller بود. حالا خودِ
    // Service هم گذار غیرمجاز رو رد می‌کنه. OnHold/Cancelled/Rejected طبق بند ۹.۲
    // ("optional controlled states") اضافه شدن.
    private static readonly Dictionary<string, string[]> LegalTransitions = new()
    {
        ["Open"] = new[] { "Assigned", "Cancelled", "Rejected" },
        ["Assigned"] = new[] { "InProgress", "OnHold", "Cancelled" },
        ["InProgress"] = new[] { "Completed", "OnHold", "Cancelled" },
        ["OnHold"] = new[] { "InProgress", "Cancelled" },
        ["Completed"] = new[] { "Closed" },
    };

    public WorkOrderLifecycleService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string? Error)> AdvanceStatusAsync(
        int workOrderId, string nextStatus, string? reason, int? changedByEmployeeId,
        int? assignedTeamId, int? assignedEmployeeId,
        string? failureModeCode, string? failureCauseCode, string? rootCause, decimal? downtimeHours,
        string? failureEffect = null, string? correctiveAction = null,
        decimal? contractorCost = null, string? contractorName = null)
    {
        var workOrder = await _context.WorkOrders
            .Include(x => x.AssignedTeam)
            .Include(x => x.AssignedEmployee)
            .FirstOrDefaultAsync(x => x.WorkOrderId == workOrderId);

        if (workOrder == null) return (false, "Work order not found.");

        var fromStatus = workOrder.Status;

        if (!LegalTransitions.TryGetValue(fromStatus, out var allowed) || !allowed.Contains(nextStatus))
            return (false, $"گذار از وضعیت «{fromStatus}» به «{nextStatus}» مجاز نیست.");

        if (nextStatus == "Assigned")
        {
            if (assignedTeamId.HasValue) workOrder.AssignedTeamId = assignedTeamId;
            if (assignedEmployeeId.HasValue) workOrder.AssignedEmployeeId = assignedEmployeeId;
        }

        if (nextStatus == "InProgress" && !workOrder.StartDate.HasValue)
            workOrder.StartDate = DateTime.Now;

        if (nextStatus == "Completed")
        {
            workOrder.CompletionDate = DateTime.Now;
            workOrder.ContractorCost = contractorCost;
            workOrder.ContractorName = contractorName;

            if (workOrder.WorkOrderType is "Corrective" or "Emergency")
            {
                workOrder.FailureModeCode = failureModeCode;
                workOrder.FailureCauseCode = failureCauseCode;
                workOrder.RootCause = rootCause;
                workOrder.DowntimeHours = downtimeHours;

                // طبق بخش ۱۰: FailureRecord نباید منبع حقیقت مستقل دوم باشد - از فیلدهای
                // ISO-14224 خودِ WorkOrder (که همین‌جا بالا ثبت شد) مشتق/همگام می‌شود.
                if (!string.IsNullOrWhiteSpace(failureModeCode))
                    await SyncFailureRecordAsync(workOrder, failureModeCode, failureCauseCode, rootCause, downtimeHours, failureEffect, correctiveAction);
            }

            // طبق بخش ۱۲: WorkOrder = تراکنش نگهداری معتبر؛ MaintenanceHistory باید از روی
            // تراکنش تکمیل‌شده مشتق شود، نه یک Entity یتیم که هیچ‌کد ی پرش نمی‌کند.
            await CreateMaintenanceHistoryAsync(workOrder);
        }

        workOrder.Status = nextStatus;

        await _context.WorkOrderStatusHistories.AddAsync(new WorkOrderStatusHistory
        {
            WorkOrderId = workOrder.WorkOrderId,
            FromStatus = fromStatus,
            ToStatus = nextStatus,
            ChangedById = changedByEmployeeId,
            ChangedDate = DateTime.Now,
            Reason = reason
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    private async Task SyncFailureRecordAsync(
        WorkOrder workOrder, string failureModeCode, string? failureCauseCode, string? rootCause, decimal? downtimeHours,
        string? failureEffect, string? correctiveAction)
    {
        var failureRecord = await _context.FailureRecords.FirstOrDefaultAsync(x => x.WorkOrderId == workOrder.WorkOrderId);

        if (failureRecord == null)
        {
            failureRecord = new FailureRecord
            {
                EquipmentId = workOrder.EquipmentId,
                WorkOrderId = workOrder.WorkOrderId,
                IsActive = true,
                CreatedDate = DateTime.Now
            };
            await _context.FailureRecords.AddAsync(failureRecord);
        }

        failureRecord.FailureDate = workOrder.CompletionDate ?? DateTime.Now;
        failureRecord.FailureMode = failureModeCode;
        failureRecord.FailureCause = failureCauseCode;
        failureRecord.RootCause = rootCause;
        failureRecord.DowntimeHours = downtimeHours;
        failureRecord.FailureEffect = failureEffect;
        failureRecord.CorrectiveAction = correctiveAction;
    }

    private async Task CreateMaintenanceHistoryAsync(WorkOrder workOrder)
    {
        var technician = workOrder.AssignedEmployee != null
            ? $"{workOrder.AssignedEmployee.FirstName} {workOrder.AssignedEmployee.LastName}"
            : workOrder.AssignedTeam?.Name ?? workOrder.AssignedTo;

        var durationHours = await _context.WorkOrderLabors
            .Where(x => x.WorkOrderId == workOrder.WorkOrderId && x.IsActive)
            .SumAsync(x => (decimal?)(x.RegularHours + x.OvertimeHours)) ?? 0;

        var laborCost = await _context.WorkOrderLabors
            .Where(x => x.WorkOrderId == workOrder.WorkOrderId && x.IsActive)
            .SumAsync(x => (decimal?)x.LaborCost) ?? 0;
        var spareCost = await _context.MaintenanceSpareUsages
            .Where(x => x.WorkOrderId == workOrder.WorkOrderId && x.IsActive)
            .SumAsync(x => (decimal?)x.TotalCost) ?? 0;

        await _context.MaintenanceHistories.AddAsync(new MaintenanceHistory
        {
            EquipmentId = workOrder.EquipmentId,
            WorkOrderId = workOrder.WorkOrderId,
            ExecutionDate = workOrder.CompletionDate ?? DateTime.Now,
            ActionTaken = workOrder.Description,
            Technician = technician,
            DurationHours = durationHours > 0 ? durationHours : null,
            Cost = laborCost + spareCost + (workOrder.ContractorCost ?? 0),
            IsActive = true,
            CreatedDate = DateTime.Now
        });
    }
}
