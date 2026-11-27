namespace PEMS.Application.Interfaces;

public sealed record WorkOrderListRow(int Id, string WorkOrderNumber, string? EquipmentTag, string WorkOrderType, string Priority, string? Team, string? Employee, string? AssignedTo, string Status, DateTime CreatedDate);
public sealed record WorkOrderCountsRow(int Id, int Open, int InProgress, int Completed, int CriticalOpen);
public sealed record WorkOrderChoice(string Value, string Label);

public sealed record WorkOrderCalendarRow(int Id, string Title, string Subtitle, string Badge, DateTime Date, DateTime End, string WorkOrderType, string Status, bool Conflict, bool Overdue, string? Team);
public sealed record WorkOrderUnscheduledRow(int Id, string WorkOrderNumber, string? Equipment, string Priority, string WorkOrderType, string? Team);

/// <summary>The answer of the new work order command: <c>NewId</c> opens the new work order (redirect with {result}).</summary>
public sealed record WorkOrderCreateAnswer(bool Success, string? Error, int NewId);

/// <summary>
/// SCRUM-831: the list of WorkOrderController.Index (all active work orders, and the ones assigned to me), its counters and the new work order (Create) as operations over the same tables and the governed lifecycle
/// (numbering, snapshot of the disciplines, workflow instance in one transaction). The details page stays on the controller for now.
/// </summary>
public interface IWorkOrderPagesOperations
{
    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "List", "WorkOrder.View")]
    Task<List<WorkOrderListRow>> ListAsync(string? status = null, string? equipmentId = null);

    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "Mine", "WorkOrder.View")]
    Task<List<WorkOrderListRow>> MineAsync(string? status = null, string? equipmentId = null);

    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "Counts", "WorkOrder.View")]
    Task<List<WorkOrderCountsRow>> CountsAsync();

    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "Create", "WorkOrder.Create")]
    Task<WorkOrderCreateAnswer> CreateAsync(int equipmentId, string workOrderType, string priority, string? description = null);

    /// <summary>The scheduled work orders of the maintenance calendar (planned from 90 days back to a year ahead) with the filters of the old page as optional text arguments (team id, employee id, status, type, priority).</summary>
    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "CalendarItems", "WorkOrder.View")]
    Task<List<WorkOrderCalendarRow>> CalendarItemsAsync(string? teamId = null, string? employeeId = null, string? status = null, string? workOrderType = null, string? priority = null);

    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "CalendarUnscheduled", "WorkOrder.View")]
    Task<List<WorkOrderUnscheduledRow>> CalendarUnscheduledAsync(string? teamId = null, string? employeeId = null, string? status = null, string? workOrderType = null, string? priority = null);

    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "CalendarTeamOptions", "WorkOrder.View")]
    Task<List<WorkOrderChoice>> CalendarTeamOptionsAsync();

    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "CalendarEmployeeOptions", "WorkOrder.View")]
    Task<List<WorkOrderChoice>> CalendarEmployeeOptionsAsync();

    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "CalendarStatusOptions", "WorkOrder.View")]
    Task<List<WorkOrderChoice>> CalendarStatusOptionsAsync();

    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "CalendarTypeOptions", "WorkOrder.View")]
    Task<List<WorkOrderChoice>> CalendarTypeOptionsAsync();

    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "CalendarPriorityOptions", "WorkOrder.View")]
    Task<List<WorkOrderChoice>> CalendarPriorityOptionsAsync();

    /// <summary>The move of a calendar card to another day: the planned start moves to that day at the same time of day and the duration stays; the lifecycle service checks stage, rights and discipline.</summary>
    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "MoveDay", "WorkOrder.View")]
    Task<(bool Success, string? Error)> MoveDayAsync(int id, DateTime date);
}
