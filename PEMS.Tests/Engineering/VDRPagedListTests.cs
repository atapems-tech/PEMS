using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» فاز ۲ (P0 - VDR): VDRService.GetPagedAsync
/// جایگزین GetAllAsync/ToListAsync بدون صفحه‌بندی در VDRController.Index شد. این تست‌ها روی
/// SQL Server واقعی اجرا می‌شوند (نه InMemory) چون ریسک اصلی این مهاجرت، ترجمه‌ی صحیح
/// زیرکوئری‌های همبسته‌ی LatestRevisionNo/LatestReviewCategory/IsOverdue توسط EF Core به SQL
/// واقعی است - چیزی که InMemory Provider اصلاً بررسی نمی‌کند.
///
/// طبق ممیزی «VDR Import — Duplicate Vendor Key Exception» بند ۶ (ردیابی منبع Vendor تکراری):
/// این کلاس قبلاً مستقیماً روی PEMSDB واقعی (CreateSqlServerOrThrow) اجرا می‌شد و SeedVendorAsync
/// بدون هیچ بررسی/پاکسازی یک Vendor جدید با نام «VDR Test Vendor {tag}» می‌ساخت - در هر اجرای کامل
/// Test Suite یک رکورد دیگر، بدون یکتایی. این دقیقاً منبع ۳۰۱ رکورد Vendor تکراری (نام‌های
/// «VDR Test Vendor A/B/C20/C50/C100/D/E»، هرکدام ~۴۳ نسخه) بود که باعث کرش واقعی
/// VDRImportService.CommitBatchAsync (ArgumentException روی Dictionary.Add) شد. حالا مثل سایر
/// تست‌های SQL واقعی که نیاز به ایزوله بودن دارند (همون الگوی CreateIsolatedSqlServerOrThrow)، یک
/// دیتابیس موقت اختصاصی می‌سازد (Migrate کامل - نه EnsureCreated، تا همون ایندکس‌های واقعی اعمال
/// شوند) و در پایان حذف می‌کند؛ PEMSDB مشترک دیگر توسط این کلاس دست نمی‌خورد.
/// </summary>
public class VDRPagedListTests : IAsyncLifetime
{
    private readonly string _dbName = "PEMS_TEST_VDRPAGED_" + Guid.NewGuid().ToString("N")[..12];
    private string _connection = "";

    public async Task InitializeAsync()
    {
        var csb = new SqlConnectionStringBuilder(PemsDbContextFactory.ConnectionString) { InitialCatalog = _dbName };
        _connection = csb.ConnectionString;
        await using var ctx = new PEMSDbContext(Options());
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        var master = new SqlConnectionStringBuilder(_connection) { InitialCatalog = "master" }.ConnectionString;
        await using var c = new SqlConnection(master);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"IF DB_ID('{_dbName}') IS NOT NULL BEGIN ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_dbName}]; END";
        await cmd.ExecuteNonQueryAsync();
    }

    private DbContextOptions<PEMSDbContext> Options() => new DbContextOptionsBuilder<PEMSDbContext>().UseSqlServer(_connection).Options;

    private PEMSDbContext NewCtx() => new(Options());

    private static VDRItem NewItem(int projectId, int vendorId, string docNo, DateTime createdDate, string status = "Submitted")
        => new()
        {
            SequenceNo = 1,
            ProjectId = projectId,
            VendorId = vendorId,
            DocumentNumber = docNo,
            DocumentDescription = $"Desc {docNo}",
            DocumentTypeCode = "DS",
            WorkflowStatus = status,
            IsActive = true,
            CreatedDate = createdDate,
            DocumentKind = "Vendor",
            DocumentNo = docNo,
            Title = docNo,
            DocumentType = "DS",
            Status = status
        };

    private static async Task<Project> SeedProjectAsync(PEMS.Persistence.Context.PEMSDbContext ctx, string tag)
    {
        var project = new Project { ProjectName = $"VDR Test {tag}", ProjectCode = $"VDR{tag}{Guid.NewGuid():N}".Substring(0, 8) };
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();
        return project;
    }

    private static async Task<Vendor> SeedVendorAsync(PEMS.Persistence.Context.PEMSDbContext ctx, string tag)
    {
        var vendor = new Vendor { Name = $"VDR Test Vendor {tag}", VendorCode = $"V{tag}{Guid.NewGuid():N}".Substring(0, 6) };
        ctx.Vendors.Add(vendor);
        await ctx.SaveChangesAsync();
        return vendor;
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsStableDisjointPages_TotalCountIndependentOfPageSize()
    {
        using var ctx = NewCtx();
        var project = await SeedProjectAsync(ctx, "A");
        var vendor = await SeedVendorAsync(ctx, "A");
        var service = new VDRService(ctx);

        for (var i = 0; i < 25; i++)
            ctx.VDRItems.Add(NewItem(project.ProjectId, vendor.VendorId, $"VDR-A-{i:00}-{Guid.NewGuid():N}".Substring(0, 20), DateTime.Now.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var page1 = await service.GetPagedAsync(project.ProjectId, null, null, 1, 20);
        var page2 = await service.GetPagedAsync(project.ProjectId, null, null, 2, 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(25, page2.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.VDRItemId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetPagedAsync_InvalidPageSize_FallsBackToDefault()
    {
        using var ctx = NewCtx();
        var project = await SeedProjectAsync(ctx, "B");
        var vendor = await SeedVendorAsync(ctx, "B");
        var service = new VDRService(ctx);

        ctx.VDRItems.Add(NewItem(project.ProjectId, vendor.VendorId, $"VDR-B-{Guid.NewGuid():N}".Substring(0, 20), DateTime.Now));
        await ctx.SaveChangesAsync();

        var result = await service.GetPagedAsync(project.ProjectId, null, null, 1, 999999);

        Assert.Equal(PEMS.Application.Common.DataGridPageSize.Default, result.PageSize);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task GetPagedAsync_AllowedPageSizes_AreHonored(int pageSize)
    {
        using var ctx = NewCtx();
        var project = await SeedProjectAsync(ctx, $"C{pageSize}");
        var vendor = await SeedVendorAsync(ctx, $"C{pageSize}");
        var service = new VDRService(ctx);

        ctx.VDRItems.Add(NewItem(project.ProjectId, vendor.VendorId, $"VDR-C-{Guid.NewGuid():N}".Substring(0, 20), DateTime.Now));
        await ctx.SaveChangesAsync();

        var result = await service.GetPagedAsync(project.ProjectId, null, null, 1, pageSize);

        Assert.Equal(pageSize, result.PageSize);
    }

    [Fact]
    public async Task GetPagedAsync_StatusFilter_ExecutesServerSide()
    {
        using var ctx = NewCtx();
        var project = await SeedProjectAsync(ctx, "D");
        var vendor = await SeedVendorAsync(ctx, "D");
        var service = new VDRService(ctx);

        ctx.VDRItems.Add(NewItem(project.ProjectId, vendor.VendorId, $"VDR-D1-{Guid.NewGuid():N}".Substring(0, 20), DateTime.Now, "Closed"));
        ctx.VDRItems.Add(NewItem(project.ProjectId, vendor.VendorId, $"VDR-D2-{Guid.NewGuid():N}".Substring(0, 20), DateTime.Now, "UnderReview"));
        await ctx.SaveChangesAsync();

        var result = await service.GetPagedAsync(project.ProjectId, null, "Closed", 1, 20);

        Assert.Single(result.Items);
        Assert.Equal("Closed", result.Items[0].WorkflowStatus);
    }

    [Fact]
    public async Task GetPagedAsync_LatestRevisionAndReviewCategory_ReflectHighestRevisionSubmission()
    {
        using var ctx = NewCtx();
        var project = await SeedProjectAsync(ctx, "E");
        var vendor = await SeedVendorAsync(ctx, "E");
        var service = new VDRService(ctx);

        var item = NewItem(project.ProjectId, vendor.VendorId, $"VDR-E-{Guid.NewGuid():N}".Substring(0, 20), DateTime.Now);
        ctx.VDRItems.Add(item);
        await ctx.SaveChangesAsync();

        ctx.VDRSubmissions.Add(new VDRSubmission { VDRItemId = item.DocumentId, RevisionNo = 0, ReviewCategory = "CO" });
        ctx.VDRSubmissions.Add(new VDRSubmission { VDRItemId = item.DocumentId, RevisionNo = 1, ReviewCategory = "AP" });
        await ctx.SaveChangesAsync();

        var result = await service.GetPagedAsync(project.ProjectId, null, null, 1, 20);

        var row = Assert.Single(result.Items);
        // طبق سند فاز ۲ بند ۹: زیرکوئری همبسته باید بالاترین RevisionNo را برگرداند، نه اولین
        Assert.Equal(1, row.LatestRevisionNo);
        Assert.Equal("AP", row.LatestReviewCategory);
    }

    [Fact]
    public async Task GetPagedAsync_EmptyDataset_ReturnsEmptyItemsWithZeroTotal()
    {
        using var ctx = NewCtx();
        var project = await SeedProjectAsync(ctx, "F");

        var service = new VDRService(ctx);

        var result = await service.GetPagedAsync(project.ProjectId, null, null, 1, 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }
}
