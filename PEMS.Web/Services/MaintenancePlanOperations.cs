using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-724: the list and the plan commands of MaintenancePlanController (it used the context inline), as operations.</summary>
public sealed class MaintenancePlanOperations(IMaintenancePlanService plans, IPMGenerationService generation, IWorkOrderLifecycleService lifecycle, PEMSDbContext context,
    IMaintenancePlanCommandService commands, IStrategyAssignmentService assignments) : IMaintenancePlanOperations
{
    public async Task<List<MaintenancePlanRow>> PlansAsync() => (await plans.GetListAsync(1, 500)).Items.Select(p => new MaintenancePlanRow(p.MaintenancePlanId, p.PlanNumber, p.EquipmentId, p.EquipmentTagNumber, p.EquipmentName, p.EquipmentClassName ?? "", p.CriticalityClass ?? "",
        p.Title, p.StrategyType ?? "", p.StrategyId, p.StrategyVersion, p.FailureModeName ?? "", p.ScheduleMode, p.HasAssignment, p.TriggerType, p.IntervalDays, p.MeterType ?? "", p.MeterInterval, p.LastExecutionMeterReading, p.NextDueMeterReading,
        p.LastExecutionDate, p.NextDueDate, p.IsOverdue)).ToList();

    // a meter-triggered plan is executed by recording the current reading, not only by a date
    public async Task<(bool Success, string? Error)> RecordMeterReadingAsync(int planId, decimal currentReading)
    {
        var plan = await context.MaintenancePlans.FirstOrDefaultAsync(x => x.MaintenancePlanId == planId);
        if (plan is null) return (false, "NotFound");
        if (plan.TriggerType is "Meter" or "Both" && plan.MeterInterval.HasValue)
        {
            plan.LastExecutionMeterReading = currentReading;
            plan.NextDueMeterReading = currentReading + plan.MeterInterval.Value;
        }
        await context.SaveChangesAsync();
        return (true, null);
    }

    // the generated work order is governed (origin snapshot of the user and the start of the lifecycle)
    private async Task<string?> GovernAsync(List<string> roles, int? employeeId)
    {
        var (ok, error, _) = await lifecycle.GovernPendingNewAsync(new WorkOrderActor(employeeId, roles));
        return ok ? null : error;
    }

    public async Task<(bool Success, string? Error)> GenerateWorkOrderAsync(List<string> userRoles, int? employeeId, int planId)
    {
        var number = await generation.GenerateForPlanAsync(planId);
        if (number is null) return (false, "این Plan یا وجود ندارد یا از قبل یک Work Order فعال (تکمیل‌نشده) دارد - برای جلوگیری از تکرار، Work Order جدید ساخته نشد.");
        var governError = await GovernAsync(userRoles, employeeId);
        return governError is null ? (true, null) : (false, governError);
    }

    public async Task<(bool Success, string? Error)> GenerateDueWorkOrdersAsync(List<string> userRoles, int? employeeId)
    {
        await generation.GenerateDueWorkOrdersAsync();
        var governError = await GovernAsync(userRoles, employeeId);
        return governError is null ? (true, null) : (false, governError);
    }

    public async Task<(bool Success, string? Error)> DeactivateAsync(int planId)
    {
        var plan = await context.MaintenancePlans.FirstOrDefaultAsync(x => x.MaintenancePlanId == planId);
        if (plan is null) return (false, "NotFound");
        plan.IsActive = false;
        await context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<MaintenancePlanAssignmentRow>> AssignmentChoicesAsync(int? equipmentId = null, int? strategyId = null) =>
        (await assignments.GetPlanEligibleAssignmentsAsync(equipmentId, strategyId)).Select(c => new MaintenancePlanAssignmentRow(c.AssignmentId, c.Tag, c.StrategyType,
            string.IsNullOrWhiteSpace(c.FailureModeName) ? (c.FailureModeNameFa ?? "") : c.FailureModeName, c.StrategyVersion, c.EquipmentClassName ?? "")).ToList();

    public async Task<List<MaintenancePlanFormRow>> FormOfAsync(int id)
    {
        var c = await commands.GetFormContextAsync(id);
        if (c is null) return [];
        return [new MaintenancePlanFormRow(c.AssignmentId, c.Equipment.Tag, $"#{c.StrategyId} v{c.StrategyVersion} {c.StrategyType}", c.FailureModeName ?? c.FailureModeNameFa ?? "",
            c.SuggestedTitle ?? "", c.RecommendedTriggerType ?? "", c.SuggestedIntervalDays,
            c.SuggestedIntervalDays.HasValue ? $"{c.SuggestedIntervalDays.Value} ({c.Equipment.CriticalityClass ?? "-"})" : "", PEMS.Domain.Common.RcmCodes.ScheduleMode.Fixed)];
    }

    public async Task<List<ReportChoiceRow>> JobPlanChoicesAsync() =>
        (await context.JobPlans.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.JobPlanNumber).Select(x => new { x.JobPlanId, x.JobPlanNumber, x.Title }).ToListAsync())
            .Select(x => new ReportChoiceRow(x.JobPlanId.ToString(System.Globalization.CultureInfo.InvariantCulture), x.JobPlanNumber + " - " + x.Title)).ToList();

    public async Task<MaintenancePlanCreateStep> CreateAsync(PlanInputDto input, List<string> userRoles, int? employeeId, string? userName)
    {
        var result = await commands.CreateAsync(input, new StrategyActor(employeeId, userName, userRoles));
        if (result.Success) return new(true, "Rcm_Info_PlanCreated", null, result.Id);
        var first = result.Errors[0];
        return new(false, first.Code, first.Args.Cast<object?>().ToArray());
    }
}
