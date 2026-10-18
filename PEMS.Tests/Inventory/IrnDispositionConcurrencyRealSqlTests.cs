using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// طبق ممیزی Material Management Step 3 بخش ۱۹/۲۱: عملیات Disposition که موجودی را تغییر
/// می‌دهند باید در برابر تایید هم‌زمان ایمن باشند - یک IRN هرگز نباید دوبار موجودی‌اش Post شود
/// چون درخواست دوباره ارسال شد (Refresh مرورگر، Double-Click، Retry). روی SQL Server واقعی
/// اجرا می‌شود چون IrnReceipt/IrnDispositionRecord از BaseEntity (RowVersion واقعی SQL Server)
/// ارث می‌برند - رفتار Concurrency واقعی روی EF InMemory Provider شبیه‌سازی نمی‌شود.
/// </summary>
public class IrnDispositionConcurrencyRealSqlTests
{
    [Fact]
    public async Task Concurrent_CompleteDispositionAsync_calls_never_post_stock_twice()
    {
        const string dbName = "PEMS_TEST_IRN_DISPOSITION_CONCURRENCY";

        await using (var seedCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(dbName))
        {
            await seedCtx.Database.EnsureDeletedAsync();
            await seedCtx.Database.MigrateAsync();

            // بدون مقداردهی صریح PK - روی SQL Server واقعی، برخلاف EF InMemory Provider، درج
            // مقدار صریح روی ستون Identity بدون IDENTITY_INSERT رد می‌شود. مقدار واقعی بعد از
            // SaveChangesAsync از خودِ Entity خوانده می‌شود.
            var vendor = new Vendor { Name = "Concurrency Vendor", IsActive = true };
            var warehouse = new Warehouse { Code = "WH-CONC", Name = "Concurrency WH", IsActive = true };
            var department = new Department { Name = "Warehouse" };
            // Employee.DisciplineId یک FK اجباری است - روی SQL Server واقعی (برخلاف InMemory)
            // باید واقعاً یک ردیف Discipline موجود باشد.
            var discipline = new Discipline { Code = "GEN", Name = "General" };
            seedCtx.Vendors.Add(vendor);
            seedCtx.Warehouses.Add(warehouse);
            seedCtx.Departments.Add(department);
            seedCtx.Disciplines.Add(discipline);
            await seedCtx.SaveChangesAsync();

            var clerk = new Employee { FirstName = "Receiving", LastName = "Clerk", DepartmentId = department.DepartmentId, DisciplineId = discipline.DisciplineId, IsActive = true };
            var qaManager = new Employee { FirstName = "QA", LastName = "Manager", DepartmentId = department.DepartmentId, DisciplineId = discipline.DisciplineId, IsActive = true };
            var material = new MaterialItem { MaterialCode = "CONC-001", Description = "Concurrency Test Material", IsActive = true, IsStockItem = true };
            seedCtx.Employees.Add(clerk);
            seedCtx.Employees.Add(qaManager);
            seedCtx.MaterialItems.Add(material);
            await seedCtx.SaveChangesAsync();

            var receipt = new IrnReceipt
            {
                IrnNumber = "IRN-2026-CONC1",
                Status = "Submitted",
                VendorId = vendor.VendorId,
                WarehouseId = warehouse.WarehouseId,
                ReceivedById = clerk.EmployeeId,
                IsActive = true,
                CreatedDate = DateTime.Now
            };
            var line = new IrnReceiptLine { MaterialItemId = material.MaterialItemId, ReceivedQuantity = 100, IsActive = true };
            receipt.Lines.Add(line);
            seedCtx.IrnReceipts.Add(receipt);
            await seedCtx.SaveChangesAsync();

            var seedSvc = new IrnReceiptService(seedCtx);
            var recordResult = await seedSvc.RecordDispositionAsync(
                receipt.IrnReceiptId, new[] { (LineId: line.IrnReceiptLineId, Accepted: 100m, Rejected: 0m, Hold: 0m) }, null, clerk.EmployeeId);
            Assert.True(recordResult.Success, recordResult.Error);

            var receiptId = receipt.IrnReceiptId;
            var qaManagerId = qaManager.EmployeeId;

            // دو فراخوانی «هم‌زمان» CompleteDispositionAsync(approved:true) روی همون IRN - دقیقاً
            // سناریوی Double-Click/Retry. هر کدام Context خودش را دارد (دقیقاً مثل دو Request
            // HTTP جدا).
            // طبق مشاهده‌ی واقعی: هر دو Task می‌توانند بازنده‌ی رقابت باشند (به ترتیب اجرای واقعی
            // Thread Pool بستگی دارد، نه به ترتیب نوشتن کد) - پس هر دو باید یکسان DbUpdateConcurrencyException
            // را مدیریت کنند، دقیقاً مثل چیزی که Controller.Approve با try/catch عمومی‌اش انجام
            // می‌دهد. این استثنا خودش اثبات می‌کند RowVersion (از BaseEntity) واقعاً کار می‌کند.
            async Task<(bool Success, string? Error)> RunOneAttemptAsync()
            {
                await using var ctx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(dbName);
                var svc = new IrnReceiptService(ctx);
                try
                {
                    return await svc.CompleteDispositionAsync(receiptId, approved: true, actorEmployeeId: qaManagerId);
                }
                catch (DbUpdateConcurrencyException)
                {
                    return (Success: false, Error: "concurrency conflict");
                }
            }

            var tasks = new[]
            {
                Task.Run(RunOneAttemptAsync),
                Task.Run(RunOneAttemptAsync)
            };

            var results = await Task.WhenAll(tasks);

            Assert.Contains(results, r => r.Success);
        }

        // این دیتابیس ایزوله فقط برای همین تست است - یک ردیف MaterialStock/InventoryTransaction
        // یعنی دقیقاً یک‌بار Post شد (نه فیلتر روی MaterialItemId، چون آن متغیر به Scope بلوک
        // seed محدود بود).
        await using var verifyCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(dbName);
        var stockRows = await verifyCtx.MaterialStocks.ToListAsync();
        Assert.Single(stockRows);
        Assert.Equal(100, stockRows[0].OnHandQuantity); // نه ۲۰۰ - دقیقاً یک‌بار Post شد

        var txCount = await verifyCtx.InventoryTransactions.CountAsync();
        Assert.Equal(1, txCount);

        await verifyCtx.Database.EnsureDeletedAsync();
    }
}
