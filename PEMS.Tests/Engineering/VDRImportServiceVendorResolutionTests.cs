using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق ممیزی «VDR Import — Duplicate Vendor Key Exception»: قبل از این رفع باگ،
/// VDRImportService.CommitBatchAsync با «ArgumentException: An item with the same key has already
/// been added» کرش می‌کرد چون Vendor های موجود را بدون تشخیص تکراری در یک Dictionary
/// (Name.ToLower() → Vendor) می‌ریخت. این تست‌ها روی SQL Server واقعی (دیتابیس ایزوله، نه PEMSDB
/// مشترک) اجرا می‌شوند - دقیقاً همون کلاس مشکلی که با داده‌ی واقعی PEMSDB (رکوردهای «VDR Test
/// Vendor C100») بازتولید و ریشه‌یابی شد.
/// </summary>
public class VDRImportServiceVendorResolutionTests : IAsyncLifetime
{
    private readonly string _dbName = "PEMS_TEST_VDRVENDOR_" + Guid.NewGuid().ToString("N")[..12];
    private string _connection = "";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

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

    private DbContextOptions<PEMSDbContext> Options(bool retry = false)
    {
        var b = new DbContextOptionsBuilder<PEMSDbContext>();
        if (retry) b.UseSqlServer(_connection, o => o.EnableRetryOnFailure());
        else b.UseSqlServer(_connection);
        return b.Options;
    }

    private PEMSDbContext NewCtx(bool retry = false) => new(Options(retry));

    private static VDRImportService NewService(PEMSDbContext ctx) => new(ctx, NullLogger<VDRImportService>.Instance);

    private static async Task<Project> SeedProjectAsync(PEMSDbContext ctx, string tag)
    {
        var project = new Project { ProjectName = $"VDR Vendor Test {tag}", ProjectCode = $"VV{tag}{Guid.NewGuid():N}"[..8] };
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();
        return project;
    }

    /// <summary>یک Batch با یک ردیف Staging برای هر (DocumentNumber, VendorName) - دقیقاً همون چیزی که StageFromExcelAsync می‌سازد.</summary>
    private static async Task<VDRImportBatch> StageAsync(PEMSDbContext ctx, int projectId, params (string DocNo, string VendorName)[] rows)
    {
        var batch = new VDRImportBatch { FileName = "test.xlsx", ProjectId = projectId, Status = "Staged", RowsTotal = rows.Length };
        ctx.VDRImportBatches.Add(batch);
        await ctx.SaveChangesAsync();

        var n = 0;
        foreach (var (docNo, vendorName) in rows)
        {
            var payload = new VDRImportRowPayload
            {
                SequenceNo = ++n,
                VendorName = vendorName,
                DocumentTypeCode = "DS",
                DocumentNumber = docNo,
                DocumentDescription = $"Desc {docNo}"
            };
            ctx.VDRImportRows.Add(new VDRImportRow
            {
                VDRImportBatchId = batch.VDRImportBatchId,
                RowNumber = n,
                DocumentNumber = docNo,
                VendorNameRaw = vendorName,
                PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
                ValidationStatus = "Valid",
                IncludeInCommit = true
            });
        }
        await ctx.SaveChangesAsync();
        return batch;
    }

    private static async Task<int> VendorCountAsync(PEMSDbContext ctx, string name)
        => await ctx.Vendors.CountAsync(v => v.Name.ToLower() == name.ToLower());

    // ---------------- Test 1: Existing unique Vendor ----------------
    [Fact]
    public async Task Existing_unique_vendor_is_reused_not_duplicated()
    {
        await using var ctx = NewCtx();
        var project = await SeedProjectAsync(ctx, "T1");
        var existing = new Vendor { Name = "Acme Fabrication", RiskLevel = "Unknown", IsActive = true };
        ctx.Vendors.Add(existing);
        await ctx.SaveChangesAsync();

        var batch = await StageAsync(ctx, project.ProjectId, ("DOC-T1-001", "Acme Fabrication"));
        var (created, updated, skipped) = await NewService(ctx).CommitBatchAsync(batch.VDRImportBatchId);

        Assert.Equal(1, created); Assert.Equal(0, updated); Assert.Equal(0, skipped);
        Assert.Equal(1, await VendorCountAsync(ctx, "Acme Fabrication"));
        var item = await ctx.VDRItems.AsNoTracking().SingleAsync(x => x.DocumentNumber == "DOC-T1-001");
        Assert.Equal(existing.VendorId, item.VendorId);
    }

    // ---------------- Test 2: New Vendor ----------------
    [Fact]
    public async Task Unknown_vendor_name_creates_exactly_one_new_vendor()
    {
        await using var ctx = NewCtx();
        var project = await SeedProjectAsync(ctx, "T2");
        var batch = await StageAsync(ctx, project.ProjectId, ("DOC-T2-001", "Brand New Vendor Ltd"));

        var (created, _, skipped) = await NewService(ctx).CommitBatchAsync(batch.VDRImportBatchId);

        Assert.Equal(1, created); Assert.Equal(0, skipped);
        Assert.Equal(1, await VendorCountAsync(ctx, "Brand New Vendor Ltd"));
    }

    // ---------------- Test 3: Same import executed twice ----------------
    [Fact]
    public async Task Committing_the_same_batch_twice_stays_idempotent_no_extra_vendor_or_item()
    {
        await using var ctx = NewCtx();
        var project = await SeedProjectAsync(ctx, "T3");
        var batch = await StageAsync(ctx, project.ProjectId, ("DOC-T3-001", "Repeat Vendor Co"));
        var svc = NewService(ctx);

        var first = await svc.CommitBatchAsync(batch.VDRImportBatchId);
        var second = await svc.CommitBatchAsync(batch.VDRImportBatchId);

        Assert.Equal((1, 0, 0), first);
        Assert.Equal((0, 1, 0), second);   // دومین Commit: همون ردیف Update می‌شود، نه Insert دوباره
        Assert.Equal(1, await VendorCountAsync(ctx, "Repeat Vendor Co"));
        Assert.Equal(1, await ctx.VDRItems.CountAsync(x => x.DocumentNumber == "DOC-T3-001"));
    }

    // ---------------- Test 4: Case/whitespace variation ----------------
    [Fact]
    public async Task Case_and_whitespace_variants_of_the_same_vendor_resolve_to_one_vendor()
    {
        await using var ctx = NewCtx();
        var project = await SeedProjectAsync(ctx, "T4");
        var batch = await StageAsync(ctx, project.ProjectId,
            ("DOC-T4-001", "Global Piping Inc"),
            ("DOC-T4-002", "  GLOBAL PIPING INC  "),
            ("DOC-T4-003", "global piping inc"));

        var (created, _, skipped) = await NewService(ctx).CommitBatchAsync(batch.VDRImportBatchId);

        Assert.Equal(3, created); Assert.Equal(0, skipped);
        Assert.Equal(1, await VendorCountAsync(ctx, "Global Piping Inc"));
        var vendorIds = await ctx.VDRItems.Where(x => x.DocumentNumber.StartsWith("DOC-T4-")).Select(x => x.VendorId).Distinct().ToListAsync();
        Assert.Single(vendorIds);
    }

    // ---------------- Test 5: Existing duplicate Vendors (the actual reported crash) ----------------
    [Fact]
    public async Task Existing_duplicate_vendor_master_data_does_not_crash_and_is_not_arbitrarily_resolved()
    {
        await using var ctx = NewCtx();
        var project = await SeedProjectAsync(ctx, "T5");
        // دقیقاً وضعیت واقعی پیدا‌شده در PEMSDB: چند Vendor با همون نام (بعد از Trim+ToLower)
        ctx.Vendors.AddRange(
            new Vendor { Name = "VDR Test Vendor C100", VendorCode = "VC1", RiskLevel = "Unknown", IsActive = true },
            new Vendor { Name = "VDR Test Vendor C100", VendorCode = "VC2", RiskLevel = "Unknown", IsActive = true },
            new Vendor { Name = "vdr test vendor c100", VendorCode = "VC3", RiskLevel = "Unknown", IsActive = true });
        await ctx.SaveChangesAsync();

        var batch = await StageAsync(ctx, project.ProjectId, ("DOC-T5-001", "VDR Test Vendor C100"));

        // این دقیقاً همون سناریوییه که قبل از رفع باگ: ArgumentException می‌انداخت
        (int created, int updated, int skipped) result = default;
        var ex = await Record.ExceptionAsync(async () => result = await NewService(ctx).CommitBatchAsync(batch.VDRImportBatchId));
        Assert.Null(ex);
        Assert.Equal((0, 0, 1), result);

        var row = await ctx.VDRImportRows.AsNoTracking().SingleAsync(x => x.DocumentNumber == "DOC-T5-001");
        Assert.Equal("Error", row.ValidationStatus);
        Assert.Contains("تعارض", row.ValidationMessage);
        Assert.DoesNotContain("Exception", row.ValidationMessage);

        Assert.False(await ctx.VDRItems.AnyAsync(x => x.DocumentNumber == "DOC-T5-001"));
        Assert.Equal(3, await VendorCountAsync(ctx, "VDR Test Vendor C100"));   // هیچ‌کدام حذف/ادغام نشد
    }

    // ---------------- Test 6: Multiple VDR rows referencing the same new Vendor ----------------
    [Fact]
    public async Task Multiple_rows_in_one_batch_referencing_a_new_vendor_share_a_single_vendor_master_record()
    {
        await using var ctx = NewCtx();
        var project = await SeedProjectAsync(ctx, "T6");
        var batch = await StageAsync(ctx, project.ProjectId,
            ("DOC-T6-001", "Shared New Vendor"), ("DOC-T6-002", "Shared New Vendor"), ("DOC-T6-003", "Shared New Vendor"));

        var (created, _, skipped) = await NewService(ctx).CommitBatchAsync(batch.VDRImportBatchId);

        Assert.Equal(3, created); Assert.Equal(0, skipped);
        Assert.Equal(1, await VendorCountAsync(ctx, "Shared New Vendor"));
    }

    // ---------------- Test 7: Concurrent creation of the same brand-new vendor name ----------------
    /// <summary>
    /// شکاف شناخته‌شده و مستندشده (نه رفع‌شده در همین Fix): هیچ ایندکس یکتای دیتابیسی روی
    /// Vendor.Name وجود ندارد (به‌عمد - چون ۳۰۱ رکورد Vendor تکراریِ از‌قبل‌موجود در PEMSDB باید
    /// اول با تصمیم مالک داده حل شوند، نه این‌جا خودکار). پس دو Commit هم‌زمان که هر دو برای اولین
    /// بار به یک نام Vendor برخورد می‌کنند، می‌توانند هرکدام Vendor خودشان را بسازند - این تست
    /// دقیقاً همین را مستند می‌کند: نه کرش، نه اتصال به Vendor غلط، ولی محافظت یکتایی سطح دیتابیس
    /// هنوز اعمال نشده.
    /// </summary>
    [Fact]
    public async Task Concurrent_commits_introducing_the_same_new_vendor_name_do_not_crash_but_db_level_uniqueness_is_a_documented_gap()
    {
        await using var seedCtx = NewCtx();
        var project = await SeedProjectAsync(seedCtx, "T7");
        var batchA = await StageAsync(seedCtx, project.ProjectId, ("DOC-T7-A", "Race Condition Vendor"));
        var batchB = await StageAsync(seedCtx, project.ProjectId, ("DOC-T7-B", "Race Condition Vendor"));

        using var barrier = new Barrier(2);
        async Task<(int, int, int)> Run(int batchId)
        {
            await using var ctx = NewCtx(retry: true);
            // هر دو کانتکست دقیقاً هم‌زمان به ذخیره‌سازی می‌رسند (بدترین حالت، بدون Sleep)
            barrier.SignalAndWait(TimeSpan.FromSeconds(30));
            return await NewService(ctx).CommitBatchAsync(batchId);
        }

        var t1 = Task.Run(() => Run(batchA.VDRImportBatchId));
        var t2 = Task.Run(() => Run(batchB.VDRImportBatchId));
        var results = await Task.WhenAll(t1, t2);

        Assert.All(results, r => Assert.Equal((1, 0, 0), r));   // هیچ‌کدام کرش نکرد یا Skip نشد

        await using var chk = NewCtx();
        var itemA = await chk.VDRItems.AsNoTracking().Include(x => x.Vendor).SingleAsync(x => x.DocumentNumber == "DOC-T7-A");
        var itemB = await chk.VDRItems.AsNoTracking().Include(x => x.Vendor).SingleAsync(x => x.DocumentNumber == "DOC-T7-B");
        Assert.Equal("Race Condition Vendor", itemA.Vendor!.Name);
        Assert.Equal("Race Condition Vendor", itemB.Vendor!.Name);   // هر دو به‌درستی نام‌گذاری شدند (حتی اگر Vendor متفاوت باشند)

        var vendorCount = await VendorCountAsync(chk, "Race Condition Vendor");
        // مستندسازی وضعیت واقعی به‌جای وانمود به تضمینی که وجود ندارد:
        Assert.InRange(vendorCount, 1, 2);
    }

    // ---------------- Test 8: Existing VDR relationships stay intact ----------------
    [Fact]
    public async Task Resolving_an_existing_vendor_for_a_new_document_does_not_disturb_its_existing_vdr_relationships()
    {
        await using var ctx = NewCtx();
        var project = await SeedProjectAsync(ctx, "T8");
        var vendor = new Vendor { Name = "Established Vendor", RiskLevel = "Unknown", IsActive = true };
        ctx.Vendors.Add(vendor);
        await ctx.SaveChangesAsync();

        var existingItem = new VDRItem
        {
            ProjectId = project.ProjectId, VendorId = vendor.VendorId, DocumentNumber = "DOC-T8-EXISTING",
            DocumentKind = "Vendor", DocumentNo = "DOC-T8-EXISTING", Title = "Existing", DocumentType = "DS",
            SequenceNo = 1, WorkflowStatus = "Closed", Status = "Closed", IsActive = true
        };
        ctx.VDRItems.Add(existingItem);
        await ctx.SaveChangesAsync();
        ctx.VDRSubmissions.Add(new VDRSubmission { VDRItemId = existingItem.DocumentId, RevisionNo = 0, ReviewCategory = "AP" });
        await ctx.SaveChangesAsync();

        var batch = await StageAsync(ctx, project.ProjectId, ("DOC-T8-NEW", "Established Vendor"));
        var (created, updated, skipped) = await NewService(ctx).CommitBatchAsync(batch.VDRImportBatchId);

        Assert.Equal(1, created); Assert.Equal(0, updated); Assert.Equal(0, skipped);
        Assert.Equal(1, await VendorCountAsync(ctx, "Established Vendor"));

        var stillExisting = await ctx.VDRItems.AsNoTracking().Include(x => x.Submissions).SingleAsync(x => x.DocumentNumber == "DOC-T8-EXISTING");
        Assert.Equal(vendor.VendorId, stillExisting.VendorId);
        Assert.Single(stillExisting.Submissions);
        Assert.Equal("Closed", stillExisting.WorkflowStatus);

        var newItem = await ctx.VDRItems.AsNoTracking().SingleAsync(x => x.DocumentNumber == "DOC-T8-NEW");
        Assert.Equal(vendor.VendorId, newItem.VendorId);
    }
}
