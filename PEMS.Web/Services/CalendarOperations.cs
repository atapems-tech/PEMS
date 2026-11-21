using PEMS.Application.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-721: the My Work day list of MaintenanceCalendarController (same service, same order); a user without an employee sees an empty list.</summary>
public sealed class CalendarOperations(ICmmsCalendarService calendar) : ICalendarOperations
{
    public async Task<List<CalendarWorkRow>> MyWorkAsync(int? employeeId, DateTime? date = null)
    {
        if (employeeId is not int employee) return [];
        var items = await calendar.GetMyWorkAsync(employee, (date ?? DateTime.Today).Date);
        return items.OrderBy(x => x.PlannedStart)
            .Select(x => new CalendarWorkRow(x.WorkOrderId, x.WorkOrderNumber, x.EquipmentTagNumber ?? "", x.EquipmentDescription ?? "", x.WorkOrderType, x.Status, x.PlannedStart, x.PlannedFinish, x.AssignedTeamName ?? "",
                x.HasEmployeeConflict ? "!" : "")).ToList();
    }
}
