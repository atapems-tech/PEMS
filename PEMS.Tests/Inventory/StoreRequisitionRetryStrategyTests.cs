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
/// <summary>
/// Phase 2 / P4 (Audit) - Program.cs پیکربندی EnableRetryOnFailure دارد؛ تراکنش کاربر در StoreRequisitionLifecycleService.RunAsync باید داخل
/// Execution Strategy باشد، وگرنه در محیط واقعی InvalidOperationException می‌دهد (همان خطایی که پیش از این اصلاح روی SQL واقعی دیده شد).
/// </summary>
public class StoreRequisitionRetryStrategyTests
{
    private const string IsolatedDbName = "PEMSDB_ConcurrencyTest";

    [Fact]
    public async Task Reserve_UnderTheProductionRetryStrategy_UsesAnExecutionStrategyAndDoesNotThrow()
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
            .UseSqlServer(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs) { InitialCatalog = IsolatedDbName }.ConnectionString, o => o.EnableRetryOnFailure()).Options);

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

}
