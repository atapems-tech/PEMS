namespace PEMS.Application.Interfaces;

/// <summary>SCRUM-718: the lists of the new MDB form (the same rows the controller put in the view bag: active projects, vendors and equipment, every purchase order).</summary>
public interface IMdbOptionsOperations
{
    [PEMS.Application.Common.PemsOperation("MdbOptions", "Projects")]
    Task<List<QcOption>> ProjectsAsync();

    [PEMS.Application.Common.PemsOperation("MdbOptions", "Vendors")]
    Task<List<QcOption>> VendorsAsync();

    [PEMS.Application.Common.PemsOperation("MdbOptions", "Equipment")]
    Task<List<QcOption>> EquipmentAsync();

    [PEMS.Application.Common.PemsOperation("MdbOptions", "PurchaseOrders")]
    Task<List<QcOption>> PurchaseOrdersAsync();
}
