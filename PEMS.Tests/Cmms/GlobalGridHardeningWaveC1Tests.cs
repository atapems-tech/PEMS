using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS — Global Grid Hardening» Wave C1 (P1): این ۶ Grid قبلاً کل جدول (بعضی‌هاشون
/// دو بار - مثل Transmittal که یک Query جدا برای SupersededBy می‌زد) بدون Skip/Take بارگذاری
/// می‌کردند. این تست‌ها GetPagedAsync تازه‌اضافه‌شده را روی هر ۶ سرویس تأیید می‌کنند: صفحه‌بندی
/// واقعی، TotalCount درست، و محاسبات Subquery همبسته (ItemsCount/ActionItemCount/...).
/// </summary>
public class GlobalGridHardeningWaveC1Tests
{
    // ================= Transmittal =================

    [Fact]
    public async Task Transmittal_GetPagedAsync_PagesCorrectly_AndComputesSupersededBy()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"WaveC1Transmittal_{nameof(Transmittal_GetPagedAsync_PagesCorrectly_AndComputesSupersededBy)}");
        var service = new TransmittalService(ctx);

        // طبق کشف تست (EF InMemory): Select به x.Project ارجاع می‌دهد - ردیف Project باید
        // واقعاً وجود داشته باشد وگرنه Provider تست (نه SQL Server واقعی) کل نتیجه را خالی می‌کند
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "P1", ProjectName = "Project One", IsActive = true });

        for (var i = 0; i < 25; i++)
        {
            ctx.Transmittals.Add(new Transmittal
            {
                TransmittalId = i + 1,
                TransmittalNo = $"TR-{i:D3}",
                ProjectId = 1,
                From = "A",
                To = "B",
                Purpose = "For Information",
                Status = "Draft",
                TransmittalDate = DateTime.Today.AddDays(-i)
            });
        }

        // زنجیره‌ی Supersedes: #1 توسط #2 جایگزین شده
        ctx.Transmittals.Add(new Transmittal
        {
            TransmittalId = 100,
            TransmittalNo = "TR-ORIG",
            ProjectId = 1,
            From = "A",
            To = "B",
            Purpose = "For Information",
            Status = "Cancelled",
            TransmittalDate = DateTime.Today.AddDays(-100)
        });
        ctx.Transmittals.Add(new Transmittal
        {
            TransmittalId = 101,
            TransmittalNo = "TR-REISSUE",
            ProjectId = 1,
            From = "A",
            To = "B",
            Purpose = "For Information",
            Status = "Draft",
            TransmittalDate = DateTime.Today.AddDays(-99),
            SupersedesTransmittalId = 100
        });

        await ctx.SaveChangesAsync();

        var page1 = await service.GetPagedAsync(1, 20);
        Assert.Equal(27, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);

        var page2 = await service.GetPagedAsync(2, 20);
        Assert.Equal(7, page2.Items.Count);

        var allIds = page1.Items.Select(x => x.TransmittalId).Concat(page2.Items.Select(x => x.TransmittalId)).ToList();
        Assert.Equal(allIds.Count, allIds.Distinct().Count());

        var orig = page1.Items.Concat(page2.Items).First(x => x.TransmittalId == 100);
        Assert.Equal(101, orig.SupersededByTransmittalId);
        Assert.Equal("TR-REISSUE", orig.SupersededByTransmittalNo);
    }

    // ================= ManufacturerDataBook (MDB) =================

    [Fact]
    public async Task MDB_GetPagedAsync_PagesCorrectly_AndAppliesFilters_AndComputesOverdue()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"WaveC1MDB_{nameof(MDB_GetPagedAsync_PagesCorrectly_AndAppliesFilters_AndComputesOverdue)}");
        var service = new ManufacturerDataBookService(ctx);

        // طبق کشف تست (EF InMemory): وقتی Select به یک Navigation Property ارجاع می‌دهد
        // (حتی با گارد != null)، ردیف والدِ FK باید واقعاً وجود داشته باشد وگرنه EF InMemory
        // (برخلاف SQL Server واقعی که LEFT JOIN می‌زند) کل نتیجه را خالی برمی‌گرداند - این یک
        // محدودیت شناخته‌شده‌ی خودِ Provider تست است، نه رفتار واقعی SQL Server.
        ctx.Equipments.Add(new Equipment { EquipmentId = 1, TagNumber = "EQ-001", Name = "EQ-001", EquipmentType = "Pump", IsActive = true });
        ctx.Vendors.Add(new Vendor { VendorId = 1, Name = "Vendor One", IsActive = true });
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "P1", ProjectName = "Project One", IsActive = true });
        ctx.Projects.Add(new Project { ProjectId = 2, ProjectCode = "P2", ProjectName = "Project Two", IsActive = true });

        for (var i = 0; i < 25; i++)
        {
            ctx.ManufacturerDataBooks.Add(new ManufacturerDataBook
            {
                MDBId = i + 1,
                EquipmentId = 1,
                VendorId = 1,
                ProjectId = i < 10 ? 1 : 2,
                Status = "Planned",
                IsActive = true,
                CreatedDate = DateTime.Today.AddDays(-i),
                // پلن گذشته -> IndexOfContentsDueDate هم گذشته و هنوز Submit نشده -> باید Overdue باشد
                PlannedFinalIssueDate = DateTime.Today.AddDays(-i)
            });
        }
        await ctx.SaveChangesAsync();

        var all = await service.GetPagedAsync(1, 20);
        Assert.Equal(25, all.TotalCount);
        Assert.Equal(20, all.Items.Count);

        var filtered = await service.GetPagedAsync(1, 50, projectId: 1);
        Assert.Equal(10, filtered.TotalCount);

        // طبق ManufacturerDataBook.IsIndexOverdue: PlannedFinalIssueDate در گذشته -> Due هم گذشته -> Overdue=true
        Assert.All(filtered.Items, x => Assert.True(x.IsIndexOverdue));
        Assert.All(filtered.Items, x => Assert.NotNull(x.IndexOfContentsDueDate));
    }

    // ================= Meeting =================

    [Fact]
    public async Task Meeting_GetPagedAsync_PagesCorrectly_AndComputesActionItemCountViaSubquery()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"WaveC1Meeting_{nameof(Meeting_GetPagedAsync_PagesCorrectly_AndComputesActionItemCountViaSubquery)}");
        var service = new MeetingService(ctx, taskService: null!);

        for (var i = 0; i < 15; i++)
        {
            ctx.Meetings.Add(new Meeting
            {
                MeetingId = i + 1,
                Title = $"Meeting {i}",
                MeetingDate = DateTime.Today.AddDays(-i),
                Status = "Scheduled",
                IsActive = true
            });
        }
        await ctx.SaveChangesAsync();

        // ۳ Task مربوط به Meeting #1، ۱ Task نامرتبط با نوع دیگر (نباید شمرده شود)
        ctx.TaskItems.Add(new TaskItem { TaskItemId = 1, Title = "AI-1", RelatedType = "Meeting", RelatedId = 1, Status = "Open" });
        ctx.TaskItems.Add(new TaskItem { TaskItemId = 2, Title = "AI-2", RelatedType = "Meeting", RelatedId = 1, Status = "Open" });
        ctx.TaskItems.Add(new TaskItem { TaskItemId = 3, Title = "AI-3", RelatedType = "Meeting", RelatedId = 1, Status = "Open" });
        ctx.TaskItems.Add(new TaskItem { TaskItemId = 4, Title = "Other", RelatedType = "Letter", RelatedId = 1, Status = "Open" });
        await ctx.SaveChangesAsync();

        var page1 = await service.GetPagedAsync(1, 10);
        Assert.Equal(15, page1.TotalCount);
        Assert.Equal(10, page1.Items.Count);

        var meeting1 = page1.Items.Single(x => x.MeetingId == 1);
        Assert.Equal(3, meeting1.ActionItemCount);
    }

    // ================= Contract =================

    [Fact]
    public async Task Contract_GetPagedAsync_PagesCorrectly()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"WaveC1Contract_{nameof(Contract_GetPagedAsync_PagesCorrectly)}");
        var service = new ContractService(ctx);

        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "P1", ProjectName = "Project One", IsActive = true });
        ctx.Vendors.Add(new Vendor { VendorId = 1, Name = "Vendor One", IsActive = true });
        await ctx.SaveChangesAsync();

        for (var i = 0; i < 22; i++)
        {
            ctx.Contracts.Add(new Contract
            {
                ContractId = i + 1,
                ContractNo = $"CT-{i:D3}",
                ProjectId = 1,
                VendorId = 1,
                Status = "Draft",
                CreatedDate = DateTime.Today.AddDays(-i)
            });
        }
        await ctx.SaveChangesAsync();

        var page1 = await service.GetPagedAsync(1, 20);
        Assert.Equal(22, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);

        var page2 = await service.GetPagedAsync(2, 20);
        Assert.Equal(2, page2.Items.Count);

        var allIds = page1.Items.Select(x => x.ContractId).Concat(page2.Items.Select(x => x.ContractId)).ToList();
        Assert.Equal(22, allIds.Distinct().Count());
    }

    // ================= Vendor =================

    [Fact]
    public async Task Vendor_GetPagedAsync_PagesCorrectly_AndIncludesInactiveVendors()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"WaveC1Vendor_{nameof(Vendor_GetPagedAsync_PagesCorrectly_AndIncludesInactiveVendors)}");
        var service = new VendorService(ctx);

        for (var i = 0; i < 24; i++)
        {
            ctx.Vendors.Add(new Vendor
            {
                VendorId = i + 1,
                Name = $"Vendor {i:D2}",
                // نیمی غیرفعال - طبق GetAllAsync موجود، فیلتر IsActive عمداً اعمال نمی‌شود
                IsActive = i % 2 == 0
            });
        }
        await ctx.SaveChangesAsync();

        var page1 = await service.GetPagedAsync(1, 20);
        Assert.Equal(24, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Contains(page1.Items, x => !x.IsActive);

        var page2 = await service.GetPagedAsync(2, 20);
        Assert.Equal(4, page2.Items.Count);
    }

    // ================= ProcurementPackage =================

    [Fact]
    public async Task ProcurementPackage_GetPagedAsync_PagesCorrectly_AndFiltersInactive()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"WaveC1ProcPkg_{nameof(ProcurementPackage_GetPagedAsync_PagesCorrectly_AndFiltersInactive)}");
        var service = new ProcurementPackageService(ctx);

        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "P1", ProjectName = "Project One", IsActive = true });
        await ctx.SaveChangesAsync();

        for (var i = 0; i < 23; i++)
        {
            ctx.ProcurementPackages.Add(new ProcurementPackage
            {
                ProcurementPackageId = i + 1,
                PackageNumber = $"PP-{i:D3}",
                ProjectId = 1,
                Title = $"Package {i}",
                Stage = "MR",
                Status = "Open",
                IsActive = true,
                CreatedDate = DateTime.Today.AddDays(-i)
            });
        }
        // این یکی نباید در نتیجه بیاید
        ctx.ProcurementPackages.Add(new ProcurementPackage
        {
            ProcurementPackageId = 999,
            PackageNumber = "PP-INACTIVE",
            ProjectId = 1,
            Title = "Inactive",
            Stage = "MR",
            Status = "Open",
            IsActive = false,
            CreatedDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        var page1 = await service.GetPagedAsync(1, 20);
        Assert.Equal(23, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.DoesNotContain(page1.Items, x => x.ProcurementPackageId == 999);

        var page2 = await service.GetPagedAsync(2, 20);
        Assert.Equal(3, page2.Items.Count);
    }
}
