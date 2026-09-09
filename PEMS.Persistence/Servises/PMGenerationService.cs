using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 6 — CMMS Operational Excellence» بند ۱۵/۵۵-۵۶: منطق تولید WorkOrder از
/// MaintenancePlanController.GenerateWorkOrder موجود این‌جا منتقل شد (Controller نازک‌تر شود،
/// طبق CLAUDE.md) و یک محافظ Idempotency واقعی اضافه شد که قبلاً وجود نداشت - قبلاً هر کلیک
/// (حتی تکراری/Double-Submit) بدون قید‌وشرط یک WorkOrder جدید می‌ساخت. Snapshot کردن
/// Task/Skill/Material از JobPlan دقیقاً همون منطق قبلی است - فقط منتقل شده، بازنویسی نشده.
/// </summary>
public class PMGenerationService : IPMGenerationService
{
    private readonly PEMSDbContext _context;
    private readonly INumberingService _numberingService;

    // طبق بند ۵۵: وضعیت‌هایی که یعنی «این WorkOrder هنوز اشغال‌کننده‌ی همین سررسید است» -
    // Closed/Cancelled/Rejected یعنی چرخه‌ی قبلی تمام شده و سررسید بعدی آزاد است.
    private static readonly string[] OccupyingStatuses = { "Open", "Assigned", "InProgress", "OnHold", "Completed" };

    public PMGenerationService(PEMSDbContext context, INumberingService numberingService)
    {
        _context = context;
        _numberingService = numberingService;
    }

    public async Task<string?> GenerateForPlanAsync(int planId)
    {
        var plan = await _context.MaintenancePlans
            .Include(x => x.Equipment)
            .FirstOrDefaultAsync(x => x.MaintenancePlanId == planId && x.IsActive);

        if (plan == null) return null;

        // طبق بند ۱۵/۵۵: محافظ Idempotency - اگر همین Plan از قبل یک WorkOrder اشغال‌کننده دارد،
        // یکی دیگر ساخته نمی‌شود (نه حتی با کلیک تکراری کاربر، نه با اجرای دوباره‌ی Batch).
        var hasOpenWorkOrder = await _context.WorkOrders
            .AnyAsync(x => x.MaintenancePlanId == planId && OccupyingStatuses.Contains(x.Status));

        if (hasOpenWorkOrder) return null;

        var workOrderNumber = await _numberingService.GetNextNumberAsync("WorkOrder");

        var workOrder = new WorkOrder
        {
            EquipmentId = plan.EquipmentId,
            MaintenancePlanId = plan.MaintenancePlanId,
            WorkOrderNumber = workOrderNumber,
            WorkOrderType = "Preventive",
            Priority = "Medium",
            Status = "Open",
            // طبق «PEMS Phase 7» بند ۱۲: سررسید فعلیِ Plan پیش از جلو رفتن (چند خط پایین‌تر)
            // این‌جا کپی می‌شود - تنها فرصت برای ثبت این مقدار، همین لحظه‌ی تولید است.
            DueDate = plan.NextDueDate,
            Description = plan.Title + (string.IsNullOrEmpty(plan.Description) ? "" : " - " + plan.Description),
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.WorkOrders.AddAsync(workOrder);
        await _context.SaveChangesAsync();

        // طبق CMMS Production Baseline v1 بند ۱۰.۴: Snapshot زمان ساخت از JobPlan - دقیقاً همون
        // منطقی که قبلاً در Controller بود، فقط منتقل‌شده (نه بازطراحی‌شده).
        if (plan.JobPlanId.HasValue)
        {
            var tasks = await _context.MaintenanceTasks
                .Where(x => x.JobPlanId == plan.JobPlanId.Value && x.IsActive)
                .OrderBy(x => x.TaskNumber)
                .ToListAsync();

            foreach (var task in tasks)
            {
                await _context.WorkOrderTasks.AddAsync(new WorkOrderTask
                {
                    WorkOrderId = workOrder.WorkOrderId,
                    TaskNumber = task.TaskNumber,
                    Title = task.Title,
                    Description = task.Description,
                    IsCompleted = false,
                    CreatedDate = DateTime.Now
                });
            }

            var requiredSkills = await _context.JobPlanRequiredSkills
                .Where(x => x.JobPlanId == plan.JobPlanId.Value)
                .ToListAsync();

            foreach (var rs in requiredSkills)
            {
                await _context.RequiredSkills.AddAsync(new RequiredSkill
                {
                    WorkOrderId = workOrder.WorkOrderId,
                    SkillId = rs.SkillId,
                    MinimumProficiencyLevel = rs.MinimumProficiencyLevel,
                    PlannedHours = rs.PlannedHours,
                    CreatedDate = DateTime.Now
                });
            }

            var requiredMaterials = await _context.JobPlanRequiredMaterials
                .Where(x => x.JobPlanId == plan.JobPlanId.Value)
                .ToListAsync();

            foreach (var rm in requiredMaterials)
            {
                await _context.WorkOrderMaterials.AddAsync(new WorkOrderMaterial
                {
                    WorkOrderId = workOrder.WorkOrderId,
                    MaterialItemId = rm.MaterialItemId,
                    RequiredQuantity = rm.RequiredQuantity,
                    UomId = rm.UomId,
                    CreatedDate = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
        }

        plan.LastExecutionDate = DateTime.Now;
        if (plan.IntervalDays.HasValue)
            plan.NextDueDate = DateTime.Now.AddDays(plan.IntervalDays.Value);

        await _context.SaveChangesAsync();

        return workOrderNumber;
    }

    public async Task<PMGenerationResultDto> GenerateDueWorkOrdersAsync()
    {
        var now = DateTime.Now;

        // طبق بند ۵۶: Overdue/Due قطعی است - فقط NextDueDate واقعی <= اکنون، نه ساعت سمت کلاینت.
        var duePlanIds = await _context.MaintenancePlans
            .Where(x => x.IsActive && x.NextDueDate.HasValue && x.NextDueDate.Value <= now)
            .Select(x => x.MaintenancePlanId)
            .ToListAsync();

        var result = new PMGenerationResultDto();

        foreach (var planId in duePlanIds)
        {
            var workOrderNumber = await GenerateForPlanAsync(planId);
            if (workOrderNumber != null)
            {
                result.WorkOrdersGenerated++;
                result.GeneratedWorkOrderNumbers.Add(workOrderNumber);
            }
            else
            {
                result.PlansSkippedAlreadyHasOpenWorkOrder++;
            }
        }

        return result;
    }
}
