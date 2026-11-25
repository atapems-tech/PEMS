using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-670, 700: the asset pages and the functional location list, the logic of the controllers and services moved without change (see <see cref="IAssetPagesOperations"/>).</summary>
public sealed class AssetPagesOperations(PEMSDbContext context, IAssetService assets, IFunctionalLocationService locations) : IAssetPagesOperations
{
    private const int RowLimit = 5000;

    public async Task<List<AssetListRow>> AssetListAsync()
        => (await assets.GetListAsync(1, RowLimit, null, null)).Items
            .Select(a => new AssetListRow(a.AssetId, a.AssetNumber, a.EquipmentTagNumber, a.EquipmentName, a.MaintenanceClass ?? "", a.Status, a.CommissioningDate, a.CriticalityClass ?? "", a.OpenWorkOrderCount, a.FunctionalLocationBreadcrumb ?? "")).ToList();

    public async Task<List<AssetHeadRow>> AssetHeadAsync(int id)
    {
        var a = await assets.GetDetailsAsync(id);
        if (a == null) return [];
        var m = a.LatestMaintenance;
        return [new(a.AssetId, a.AssetNumber, a.EquipmentTagNumber, a.EquipmentName, $"{a.Manufacturer} / {a.Model}", a.FunctionalLocationBreadcrumb ?? "", a.FunctionalLocation ?? "", a.MaintenanceClass ?? "", a.CommissioningDate, a.Status,
            a.CriticalityClass ?? "", a.RiskLevel ?? "", a.IsActive, a.Description ?? "", a.WorkOrders.Count(w => w.Status != "Completed" && w.Status != "Closed"), a.TotalCost, m?.ExecutionDate, m?.ActionTaken ?? "", m?.Technician ?? "",
            m?.DurationHours?.ToString("N1") ?? "")];
    }

    public async Task<List<AssetWorkOrderRow>> AssetWorkOrdersAsync(int id)
        => (await assets.GetDetailsAsync(id))?.WorkOrders.Select(w => new AssetWorkOrderRow(w.WorkOrderId, w.WorkOrderNo ?? "", w.Description ?? "", w.Status, w.CreatedDate)).ToList() ?? [];

    public async Task<List<AssetInspectionRow>> AssetInspectionsAsync(int id)
        => (await assets.GetDetailsAsync(id))?.Inspections.Select(i => new AssetInspectionRow(i.InspectionRecordId, i.InspectionType ?? "", i.InspectionDate, i.Inspector ?? "", i.Result ?? "")).ToList() ?? [];

    public async Task<List<AssetFailureRow>> AssetFailuresAsync(int id)
        => (await assets.GetDetailsAsync(id))?.Failures.Select(f => new AssetFailureRow(f.FailureRecordId, f.FailureDate, f.FailureMode, f.FailureCause ?? "", f.DowntimeHours?.ToString("N1") ?? "")).ToList() ?? [];

    public async Task<List<PickerOption>> LocationOptionsAsync()
        => (await locations.GetPickerListAsync()).Select(l => new PickerOption(l.FunctionalLocationId.ToString(), l.PathBreadcrumb)).ToList();

    public async Task<TreeNodesAnswer> TreeNodesAsync(int? parentId = null, string? q = null, int page = 1)
    {
        var result = await locations.GetTreeNodesAsync(parentId, q, page, 20);
        return new(result.Items.Select(i => new TreeNodeItem(i.Id.ToString(), i.Label, i.Sub, i.HasChildren)).ToList(), result.HasMore);
    }

    public async Task<List<LocationRow>> LocationListAsync()
        => (await locations.GetPagedListAsync(1, RowLimit)).Items
            .Select(l => new LocationRow(l.FunctionalLocationId, l.Code, l.Name, l.Plant ?? "", l.Area ?? "", l.Unit ?? "", l.ParentCode ?? "", l.PathBreadcrumb, l.EquipmentCount, l.AssetCount, l.IsActive)).ToList();

    // the rules of the service update that an entity write does not know: a unique active code, a location is not its own parent, no cycle in the tree
    public async Task<(bool Success, string? Error)> ValidateLocationAsync(int? id, string code, int? parentId = null)
    {
        code = code?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(code)) return (false, "کد Functional Location الزامی است.");
        if (await context.FunctionalLocations.AnyAsync(x => x.FunctionalLocationId != id && x.Code == code && x.IsActive)) return (false, $"کد «{code}» قبلاً استفاده شده است.");
        if (parentId.HasValue && id.HasValue)
        {
            if (parentId == id) return (false, "یک Functional Location نمی‌تواند Parent خودش باشد.");
            var byId = (await context.FunctionalLocations.AsNoTracking().Select(x => new { x.FunctionalLocationId, x.ParentId }).ToListAsync()).ToDictionary(x => x.FunctionalLocationId, x => x.ParentId);
            var cursor = (int?)parentId.Value;
            while (cursor.HasValue)
            {
                if (cursor.Value == id.Value) return (false, "این انتخاب باعث یک حلقه در ساختار درختی می‌شود.");
                byId.TryGetValue(cursor.Value, out cursor);
            }
        }
        if (parentId.HasValue && !await context.FunctionalLocations.AnyAsync(x => x.FunctionalLocationId == parentId.Value && x.IsActive)) return (false, "Parent انتخاب‌شده نامعتبره.");
        return (true, null);
    }
}
