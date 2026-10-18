using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// Race واقعی روی SQL Server ایزوله (همان الگوی سایر تست‌های Concurrency): دو درخواست هم‌زمان برای آخرین موجودی.
/// MaterialStock دارای RowVersion است؛ سرویس باید یکی را بپذیرد و دیگری را (با پیام قابل‌فهم) رد کند و هیچ‌گاه Reserved > OnHand نشود.
/// </summary>
public class StoreRequisitionReservationConcurrencyTests
{
    private const string IsolatedDbName = "PEMSDB_ConcurrencyTest";

    [Fact]
    public async Task TwoConcurrentReservationsForTheLastStock_OnlyOneSucceeds_AndInvariantHolds()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        int itemId, req1, req2, empId;

        await using (var seed = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName))
        {
            var dept = new Department { Name = "CC-" + tag, Code = tag, IsActive = true };
            var disc = new Discipline { Name = "CC-" + tag, Code = tag[..6], IsActive = true };
            seed.Departments.Add(dept); seed.Disciplines.Add(disc);
            await seed.SaveChangesAsync();
            var emp = new Employee { FirstName = "CC", LastName = tag, DepartmentId = dept.DepartmentId, DisciplineId = disc.DisciplineId, IsActive = true };
            var wh = new Warehouse { Code = "W" + tag, Name = "CC " + tag, IsActive = true };
            var item = new MaterialItem { MaterialCode = "M" + tag, Description = "cc item " + tag, IsActive = true };
            seed.AddRange(emp, wh, item);
            await seed.SaveChangesAsync();
            seed.MaterialStocks.Add(new MaterialStock { MaterialItemId = item.MaterialItemId, WarehouseId = wh.WarehouseId, OnHandQuantity = 10, IsActive = true });

            StoreRequisition Make(string n)
            {
                var r = new StoreRequisition { RequisitionNumber = $"CC-{tag}-{n}", WarehouseId = wh.WarehouseId, RequestedById = emp.EmployeeId, Status = StoreRequisitionStatuses.Approved };
                r.Lines.Add(new StoreRequisitionLine { MaterialItemId = item.MaterialItemId, RequestedQuantity = 8, ApprovedQuantity = 8 });
                return r;
            }
            var a = Make("A"); var b = Make("B");
            seed.StoreRequisitions.AddRange(a, b);
            await seed.SaveChangesAsync();
            empId = emp.EmployeeId; itemId = item.MaterialItemId; req1 = a.StoreRequisitionId; req2 = b.StoreRequisitionId;
        }

        var cs = PemsDbContextFactory.ConnectionString;
        PEMSDbContext Open() => new(new DbContextOptionsBuilder<PEMSDbContext>()
            .UseSqlServer(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs) { InitialCatalog = IsolatedDbName }.ConnectionString).Options);

        await using var c1 = Open();
        await using var c2 = Open();
        var s1 = new StoreRequisitionLifecycleService(c1, new StoreRequisitionLifecycleServiceTests.MapActions());
        var s2 = new StoreRequisitionLifecycleService(c2, new StoreRequisitionLifecycleServiceTests.MapActions());

        var results = await Task.WhenAll(
            Task.Run(() => s1.AdvanceStatusAsync(req1, StoreRequisitionStatuses.Reserved, null, StoreRequisitionLifecycleServiceTests.A(empId))),
            Task.Run(() => s2.AdvanceStatusAsync(req2, StoreRequisitionStatuses.Reserved, null, StoreRequisitionLifecycleServiceTests.A(empId))));

        Assert.Equal(1, results.Count(r => r.Success));
        Assert.False(string.IsNullOrWhiteSpace(results.Single(r => !r.Success).Error));

        await using var verify = Open();
        var stock = await verify.MaterialStocks.AsNoTracking().SingleAsync(s => s.MaterialItemId == itemId);
        Assert.Equal(10, stock.OnHandQuantity);
        Assert.Equal(8, stock.ReservedQuantity);
        Assert.Equal(1, await verify.MaterialReservations.CountAsync(r => r.MaterialItemId == itemId));
        Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t => t.MaterialItemId == itemId && t.TransactionType == "Reservation"));
    }

    private sealed record Seeded(int ReqId, int LineId, int ItemId, int Requester, int Approver, int Issuer);

    private static PEMSDbContext Open() => new(new DbContextOptionsBuilder<PEMSDbContext>()
        .UseSqlServer(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(PemsDbContextFactory.ConnectionString) { InitialCatalog = IsolatedDbName }.ConnectionString).Options);

    /// <summary>درخواست Reserved با Approved=70 از موجودی 100 روی دیتابیس ایزوله (با همان سرویس واقعی)</summary>
    private static async Task<Seeded> SeedReservedAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        await using var seed = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
        var dept = new Department { Name = "PI-" + tag, Code = tag, IsActive = true };
        var disc = new Discipline { Name = "PI-" + tag, Code = tag[..6], IsActive = true };
        seed.Departments.Add(dept); seed.Disciplines.Add(disc);
        await seed.SaveChangesAsync();
        Employee E(string n) => new() { FirstName = n, LastName = tag, DepartmentId = dept.DepartmentId, DisciplineId = disc.DisciplineId, IsActive = true };
        var requester = E("req"); var approver = E("app"); var issuer = E("iss");
        var wh = new Warehouse { Code = "P" + tag, Name = "PI " + tag, IsActive = true };
        var item = new MaterialItem { MaterialCode = "P" + tag, Description = "pi " + tag, IsActive = true };
        seed.AddRange(requester, approver, issuer, wh, item);
        await seed.SaveChangesAsync();
        seed.MaterialStocks.Add(new MaterialStock { MaterialItemId = item.MaterialItemId, WarehouseId = wh.WarehouseId, OnHandQuantity = 100, IsActive = true });
        var req = new StoreRequisition { RequisitionNumber = $"PI-{tag}", WarehouseId = wh.WarehouseId, RequestedById = requester.EmployeeId, Status = StoreRequisitionStatuses.Submitted };
        req.Lines.Add(new StoreRequisitionLine { MaterialItemId = item.MaterialItemId, RequestedQuantity = 100 });
        seed.StoreRequisitions.Add(req);
        await seed.SaveChangesAsync();

        var svc = new StoreRequisitionLifecycleService(seed, new StoreRequisitionLifecycleServiceTests.MapActions());
        var lineId = req.Lines.Single().StoreRequisitionLineId;
        Assert.True((await svc.SetApprovedQuantitiesAsync(req.StoreRequisitionId, new Dictionary<int, decimal> { [lineId] = 70 }, StoreRequisitionLifecycleServiceTests.A(approver.EmployeeId))).Success);
        Assert.True((await svc.AdvanceStatusAsync(req.StoreRequisitionId, "Approved", null, StoreRequisitionLifecycleServiceTests.A(approver.EmployeeId))).Success);
        var r = await svc.AdvanceStatusAsync(req.StoreRequisitionId, "Reserved", null, StoreRequisitionLifecycleServiceTests.A(approver.EmployeeId));
        Assert.True(r.Success, r.Error);
        return new Seeded(req.StoreRequisitionId, lineId, item.MaterialItemId, requester.EmployeeId, approver.EmployeeId, issuer.EmployeeId);
    }

    [Fact]
    public async Task TwoConcurrentPartialIssues_NeverIssueMoreThanReserved_AndStayConsistent()
    {
        var s = await SeedReservedAsync();                          // Approved=Reserved=70
        await using var c1 = Open(); await using var c2 = Open();
        var s1 = new StoreRequisitionLifecycleService(c1, new StoreRequisitionLifecycleServiceTests.MapActions());
        var s2 = new StoreRequisitionLifecycleService(c2, new StoreRequisitionLifecycleServiceTests.MapActions());
        var actor = StoreRequisitionLifecycleServiceTests.A(s.Issuer);

        var results = await Task.WhenAll(
            Task.Run(() => s1.IssueAsync(s.ReqId, new Dictionary<int, decimal> { [s.LineId] = 50 }, null, actor, Guid.NewGuid().ToString("N"))),
            Task.Run(() => s2.IssueAsync(s.ReqId, new Dictionary<int, decimal> { [s.LineId] = 50 }, null, actor, Guid.NewGuid().ToString("N"))));

        Assert.Equal(1, results.Count(r => r.Success));             // 50+50 > 70
        await using var v = Open();
        var line = await v.StoreRequisitionLines.AsNoTracking().SingleAsync(l => l.StoreRequisitionLineId == s.LineId);
        var stock = await v.MaterialStocks.AsNoTracking().SingleAsync(x => x.MaterialItemId == s.ItemId);
        Assert.Equal(50, line.IssuedQuantity);
        Assert.Equal((50m, 20m), (stock.OnHandQuantity, stock.ReservedQuantity));
        Assert.Equal(1, await v.InventoryTransactions.CountAsync(t => t.MaterialItemId == s.ItemId && t.TransactionType == "Issue"));
    }

    [Fact]
    public async Task SameRequestKeyIssuedConcurrently_AppliesExactlyOnce()
    {
        var s = await SeedReservedAsync();
        await using var c1 = Open(); await using var c2 = Open();
        var s1 = new StoreRequisitionLifecycleService(c1, new StoreRequisitionLifecycleServiceTests.MapActions());
        var s2 = new StoreRequisitionLifecycleService(c2, new StoreRequisitionLifecycleServiceTests.MapActions());
        var actor = StoreRequisitionLifecycleServiceTests.A(s.Issuer);
        var key = Guid.NewGuid().ToString("N");

        var results = await Task.WhenAll(
            Task.Run(() => s1.IssueAsync(s.ReqId, new Dictionary<int, decimal> { [s.LineId] = 30 }, null, actor, key)),
            Task.Run(() => s2.IssueAsync(s.ReqId, new Dictionary<int, decimal> { [s.LineId] = 30 }, null, actor, key)));

        Assert.All(results, r => Assert.True(r.Success, r.Error));  // دومی «قبلاً انجام شده» = موفق
        await using var v = Open();
        Assert.Equal(30, (await v.StoreRequisitionLines.AsNoTracking().SingleAsync(l => l.StoreRequisitionLineId == s.LineId)).IssuedQuantity);
        Assert.Equal(1, await v.InventoryTransactions.CountAsync(t => t.MaterialItemId == s.ItemId && t.TransactionType == "Issue"));
        Assert.Equal(1, await v.StoreRequisitionStatusHistories.CountAsync(h => h.StoreRequisitionId == s.ReqId && h.RequestKey == key));
    }
}
