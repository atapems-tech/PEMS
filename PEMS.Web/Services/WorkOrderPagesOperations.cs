using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-831: the work order list, its counters and the new work order of WorkOrderController as operations over the same tables and lifecycle service (see <see cref="IWorkOrderPagesOperations"/>).</summary>
public sealed class WorkOrderPagesOperations(PEMSDbContext context, ICurrentUserService currentUser, IWorkOrderLifecycleService lifecycle, INumberingService numbering, ICmmsCalendarService calendar) : IWorkOrderPagesOperations
{
    private const int Take = 5000;
    private static readonly string[] ValidWorkOrderTypes = { "Preventive", "Corrective", "Emergency", "Predictive" };
    private static readonly string[] ValidPriorities = { "Low", "Medium", "High", "Critical" };

    private static bool Fa => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa";

    private async Task<int?> EmployeeIdAsync()
        => currentUser.UserId.HasValue ? await context.Users.Where(u => u.Id == currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync() : null;

    private IQueryable<PEMS.Domain.Entities.WorkOrder> Active() => context.WorkOrders.AsNoTracking().Where(x => x.IsActive);

    private static IQueryable<WorkOrderListRow> Rows(IQueryable<PEMS.Domain.Entities.WorkOrder> q)
        => q.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.WorkOrderId).Take(Take).Select(x => new WorkOrderListRow(x.WorkOrderId, x.WorkOrderNumber, x.Equipment != null ? x.Equipment.TagNumber : null, x.WorkOrderType, x.Priority,
            x.AssignedTeam != null ? x.AssignedTeam.Name : null, x.AssignedEmployee != null ? x.AssignedEmployee.FirstName + " " + x.AssignedEmployee.LastName : null, x.AssignedTo, x.Status, x.CreatedDate));

    // the filters of the address (?status=Open&equipmentId=7) keep working: the page passes them to the list; empty means no filter
    private IQueryable<PEMS.Domain.Entities.WorkOrder> Filtered(IQueryable<PEMS.Domain.Entities.WorkOrder> q, string? status, string? equipmentId)
    {
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
        if (int.TryParse(equipmentId, out var eq)) q = q.Where(x => x.EquipmentId == eq);
        return q;
    }

    public async Task<List<WorkOrderListRow>> ListAsync(string? status = null, string? equipmentId = null) => await Rows(Filtered(Active(), status, equipmentId)).ToListAsync();

    public async Task<List<WorkOrderListRow>> MineAsync(string? status = null, string? equipmentId = null)
    {
        var employeeId = await EmployeeIdAsync();
        return await Rows(Filtered(Active().Where(x => x.AssignedEmployeeId == employeeId), status, equipmentId)).ToListAsync();
    }

    public async Task<List<WorkOrderCountsRow>> CountsAsync()
    {
        var q = Active();
        return [new WorkOrderCountsRow(1, await q.CountAsync(x => x.Status == "Open"), await q.CountAsync(x => x.Status == "InProgress" || x.Status == "Assigned"),
            await q.CountAsync(x => x.Status == "Completed" || x.Status == "Closed"), await q.CountAsync(x => x.Priority == "Critical" && x.Status != "Completed" && x.Status != "Closed"))];
    }

    public async Task<WorkOrderCreateAnswer> CreateAsync(int equipmentId, string workOrderType, string priority, string? description = null)
    {
        // the same checks, with the same canonical values as the form offers
        if (equipmentId <= 0 || !await context.Equipments.AnyAsync(x => x.EquipmentId == equipmentId && x.IsActive))
            return new(false, "تجهیز انتخاب‌شده نامعتبره.", 0);
        workOrderType = workOrderType?.Trim() ?? string.Empty;
        priority = priority?.Trim() ?? string.Empty;
        if (!ValidWorkOrderTypes.Contains(workOrderType, StringComparer.Ordinal)) return new(false, "نوع WorkOrder نامعتبره.", 0);
        if (!ValidPriorities.Contains(priority, StringComparer.Ordinal)) return new(false, "اولویت نامعتبره.", 0);

        // a new work order is always governed: snapshot of the disciplines and the lifecycle instance with an explicit binding, in one transaction; the number comes from the numbering service
        var workOrderNumber = await numbering.GetNextNumberAsync("WorkOrder");
        var (created, error, newId) = await lifecycle.CreateGovernedAsync(new WorkOrderCreateRequest
        {
            WorkOrderNumber = workOrderNumber, EquipmentId = equipmentId, WorkOrderType = workOrderType, Priority = priority, Description = description
        }, new WorkOrderActor(await EmployeeIdAsync(), currentUser.Roles));
        return created && newId.HasValue ? new(true, null, newId.Value) : new(false, error, 0);
    }

    private async Task<MaintenanceCalendarResultDto> CalendarAsync(string? teamId, string? employeeId, string? status, string? workOrderType, string? priority, bool unscheduled)
        => await calendar.GetCalendarAsync(DateTime.Today.AddDays(-90), DateTime.Today.AddDays(365), int.TryParse(teamId, out var t) ? t : null, int.TryParse(employeeId, out var e) ? e : null,
            string.IsNullOrWhiteSpace(status) ? null : status, string.IsNullOrWhiteSpace(workOrderType) ? null : workOrderType, string.IsNullOrWhiteSpace(priority) ? null : priority, null, includeUnscheduled: unscheduled);

    public async Task<List<WorkOrderCalendarRow>> CalendarItemsAsync(string? teamId = null, string? employeeId = null, string? status = null, string? workOrderType = null, string? priority = null)
        => (await CalendarAsync(teamId, employeeId, status, workOrderType, priority, false)).Items.Select(i => new WorkOrderCalendarRow(i.WorkOrderId, i.WorkOrderNumber + (string.IsNullOrEmpty(i.EquipmentTagNumber) ? "" : " - " + i.EquipmentTagNumber),
            string.Join(" - ", new[] { i.AssignedTeamName, i.AssignedEmployeeName }.Where(x => !string.IsNullOrEmpty(x))), i.Priority, i.PlannedStart, i.PlannedFinish, i.WorkOrderType, i.Status, i.HasEmployeeConflict, i.IsOverdue, i.AssignedTeamName)).ToList();

    public async Task<List<WorkOrderUnscheduledRow>> CalendarUnscheduledAsync(string? teamId = null, string? employeeId = null, string? status = null, string? workOrderType = null, string? priority = null)
        => (await CalendarAsync(teamId, employeeId, status, workOrderType, priority, true)).Unscheduled.Select(u => new WorkOrderUnscheduledRow(u.WorkOrderId, u.WorkOrderNumber, u.EquipmentTagNumber, u.Priority, u.WorkOrderType, u.AssignedTeamName)).ToList();

    public async Task<List<WorkOrderChoice>> CalendarTeamOptionsAsync()
        => [new WorkOrderChoice("", "-"), .. (await calendar.GetActiveTeamsAsync()).Select(t => new WorkOrderChoice(t.Id.ToString(), t.Name))];

    public async Task<List<WorkOrderChoice>> CalendarEmployeeOptionsAsync()
        => [new WorkOrderChoice("", "-"), .. await context.Employees.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FirstName).ThenBy(x => x.LastName).Select(x => new WorkOrderChoice(x.EmployeeId.ToString(), x.FirstName + " " + x.LastName)).ToListAsync()];

    public Task<List<WorkOrderChoice>> CalendarStatusOptionsAsync() => Task.FromResult(new[] { "", "Open", "Assigned", "InProgress", "OnHold", "Completed", "Closed" }.Select(x => new WorkOrderChoice(x, x == "" ? "-" : x)).ToList());

    public Task<List<WorkOrderChoice>> CalendarTypeOptionsAsync() => Task.FromResult(new[] { "", "Preventive", "Corrective", "Emergency", "Predictive" }.Select(x => new WorkOrderChoice(x, x == "" ? "-" : x)).ToList());

    public Task<List<WorkOrderChoice>> CalendarPriorityOptionsAsync() => Task.FromResult(new[] { "", "Low", "Medium", "High", "Critical" }.Select(x => new WorkOrderChoice(x, x == "" ? "-" : x)).ToList());

    public async Task<(bool Success, string? Error)> MoveDayAsync(int id, DateTime date)
    {
        var wo = await context.WorkOrders.AsNoTracking().Where(x => x.WorkOrderId == id).Select(x => new { x.PlannedStart, x.PlannedFinish }).FirstOrDefaultAsync();
        if (wo?.PlannedStart is not { } start || wo.PlannedFinish is not { } finish) return (false, "Not found");
        var newStart = date.Date + start.TimeOfDay;
        return await lifecycle.ScheduleWorkOrderAsync(id, newStart, newStart + (finish - start), new WorkOrderActor(await EmployeeIdAsync(), currentUser.Roles));
    }
}
