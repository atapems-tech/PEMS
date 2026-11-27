namespace PEMS.Application.Interfaces;

public sealed record WorkOrderListRow(int Id, string WorkOrderNumber, string? EquipmentTag, string WorkOrderType, string Priority, string? Team, string? Employee, string? AssignedTo, string Status, DateTime CreatedDate);
public sealed record WorkOrderCountsRow(int Id, int Open, int InProgress, int Completed, int CriticalOpen);
public sealed record WorkOrderChoice(string Value, string Label);

/// <summary>The answer of the new work order command: <c>NewId</c> opens the new work order (redirect with {result}).</summary>
public sealed record WorkOrderCreateAnswer(bool Success, string? Error, int NewId);

/// <summary>
/// SCRUM-831: the list of WorkOrderController.Index (all active work orders, and the ones assigned to me), its counters and the new work order (Create) as operations over the same tables and the governed lifecycle
/// (numbering, snapshot of the disciplines, workflow instance in one transaction). The details page stays on the controller for now.
/// </summary>
public interface IWorkOrderPagesOperations
{
    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "List", "WorkOrder.View")]
    Task<List<WorkOrderListRow>> ListAsync();

    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "Mine", "WorkOrder.View")]
    Task<List<WorkOrderListRow>> MineAsync();

    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "Counts", "WorkOrder.View")]
    Task<List<WorkOrderCountsRow>> CountsAsync();

    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "EquipmentOptions", "WorkOrder.Create")]
    Task<List<WorkOrderChoice>> EquipmentOptionsAsync();

    [PEMS.Application.Common.PemsOperation("WorkOrderPage", "Create", "WorkOrder.Create")]
    Task<WorkOrderCreateAnswer> CreateAsync(int equipmentId, string workOrderType, string priority, string? description = null);
}
