namespace PEMS.Application.Interfaces;

public sealed record CalendarWorkRow(int WorkOrderId, string WorkOrderNumber, string Equipment, string Description, string WorkOrderType, string Status, DateTime PlannedStart, DateTime PlannedFinish, string Team, string Conflict);

/// <summary>
/// SCRUM-721: the "My Work" day list of the maintenance calendar as a read operation (the planned work of the current user's employee on a day, default today). The drag and drop of the calendar
/// reschedules through the existing action definition work-order-set-schedule (the same domain operation and the same work order section guard as the work order page), so no separate operation exists for it.
/// The month, week and day grid (Index) stays on MaintenanceCalendarController until the engine has a calendar component (SCRUM-818).
/// </summary>
public interface ICalendarOperations
{
    [PEMS.Application.Common.PemsOperation("CalendarPage", "MyWork", "WorkOrder.View")]
    Task<List<CalendarWorkRow>> MyWorkAsync(int? employeeId, DateTime? date = null);
}
