using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Application.Inventory;
using PEMS.Application.Replenishment;

namespace PEMS.Tests.Inventory;

/// <summary>ابزار مشترک تست‌های P5: مجوز نقش-محور ساده، P3 جعلی با نتایج واقعیِ NetDemandCalculator، و ساخت ورودی‌های عددی.</summary>
internal static class ReplenishmentTestKit
{
    public const string Module = IReplenishmentRecommendationService.Module;

    public sealed class Perms : IPermissionService
    {
        public HashSet<(string Role, string Module, string Action)> Grants { get; } = new();
        public Task<bool> HasPermissionAsync(IEnumerable<string> userRoles, string moduleName, string action) =>
            Task.FromResult(userRoles.Any(r => Grants.Contains((r, moduleName, action))));
        public Task<List<RolePermissionDto>> GetMatrixAsync() => throw new NotSupportedException();
        public Task SetPermissionsAsync(string roleName, IEnumerable<ModulePermissionInput> permissions) => throw new NotSupportedException();
        public Task SeedDefaultsAsync() => throw new NotSupportedException();

        public Perms Allow(string role, string module, params string[] actions)
        {
            foreach (var a in actions) Grants.Add((role, module, a));
            return this;
        }
    }

    /// <summary>جایگزین INetDemandService که لیست نتایج را بدون پوشاندن برمی‌گرداند و Scope را مانند سرویس واقعی فقط به‌عنوان فیلتر اعمال می‌کند.</summary>
    public sealed class FakeNetDemand : INetDemandService
    {
        public List<NetDemandResult> Results { get; set; } = new();
        public int Calls;
        public Task<NetDemandReport> CalculateAsync(NetDemandScope scope, NetDemandCaller caller, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<NetDemandResult>> CalculateUnmaskedForSystemAsync(NetDemandScope scope, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Calls);
            IEnumerable<NetDemandResult> q = Results;
            if (scope?.MaterialItemIds is { Count: > 0 }) q = q.Where(r => scope.MaterialItemIds.Contains(r.MaterialItemId));
            if (scope?.WarehouseIds is { Count: > 0 }) q = q.Where(r => r.WarehouseId.HasValue && scope.WarehouseIds.Contains(r.WarehouseId.Value));
            return Task.FromResult<IReadOnlyList<NetDemandResult>>(q.ToList());
        }
    }

    public static readonly ReplenishmentActor Full = new(1, 1, new[] { "REPL" });

    public static Perms StandardPerms() => new Perms()
        .Allow("REPL", Module, "View", "Create", "Edit")
        .Allow("REPL", "StoreRequisition", "View")
        .Allow("REPL", "WorkOrder", "View");

    public static DemandLine SrLine(int lineId, int item, int wh, decimal qty, decimal covered = 0m, int parent = 1) =>
        new(new DemandOrigin(DemandSourceType.StoreRequisitionLine, lineId, parent, "SR-" + parent), item, wh, qty, covered);

    public static UsableStockResult Stock(int item, int wh, decimal onHand, decimal reserved = 0m, params NetDemandIssueCode[] issues) =>
        new(item, wh, StockState.Ok, onHand, reserved, onHand - reserved, 1, null, issues.Select(i => new NetDemandIssue(i)).ToList());

    /// <summary>نتیجه‌ی واقعی NetDemandCalculator برای یک (متریال، انبار).</summary>
    public static NetDemandResult Result(int item, int wh, UsableStockResult? stock, params DemandLine[] demand) =>
        NetDemandCalculator.Calculate(new NetDemandInput(item, wh, stock, demand, Array.Empty<ExcludedDemand>()));

    public static NetDemandResult AmbiguousStock(int item, int wh, params DemandLine[] demand) =>
        NetDemandCalculator.Calculate(new NetDemandInput(item, wh,
            new UsableStockResult(item, wh, StockState.AmbiguousRows, null, null, null, 2, null, new[] { new NetDemandIssue(NetDemandIssueCode.StockAmbiguousRows) }),
            demand, Array.Empty<ExcludedDemand>()));

    /// <summary>نتیجه‌ی بی‌انبار (نیاز Work Order): هرگز با موجودی مقایسه نمی‌شود.</summary>
    public static NetDemandResult NoWarehouse(int item, decimal qty) =>
        NetDemandCalculator.Calculate(new NetDemandInput(item, null, null,
            new[] { new DemandLine(new DemandOrigin(DemandSourceType.WorkOrderMaterial, 9, 3, "WO-3"), item, null, qty, 0m) }, Array.Empty<ExcludedDemand>()));
}
