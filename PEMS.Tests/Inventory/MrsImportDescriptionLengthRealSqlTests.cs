using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// طبق ممیزی «MaterialItems.Description String-Length Integrity»: EF InMemory Provider هرگز محدودیت
/// nvarchar(N) را واقعاً اعمال نمی‌کند - یعنی تست‌های MrsImportDescriptionLengthTests (InMemory) حتی
/// قبل از رفع باگ هم سبز می‌شدند و به‌تنهایی ثابت نمی‌کنند Migration واقعاً روی SQL Server اثر کرده.
/// این کلاس دقیقاً همان مقدار واقعیِ گزارش‌شده در Exception اصلی تولید را روی یک SQL Server واقعی
/// ایزوله Commit می‌کند و مقدار Persist شده را مستقیماً با Query دوباره می‌خواند تا برابری حرف‌به‌حرف
/// را ثابت کند - نه فقط عدم-Exception را.
/// </summary>
public class MrsImportDescriptionLengthRealSqlTests
{
    private const string RealReportedDescription =
        "کابل کاربرد ابزار دقیق و کنترل نوع ولتاژ ابزار دقیق و کنترل سطح ولتاژ 300/500 رنگ مشکی با خط قرمز شیلد شیلد دار آرمورد آرمورد دار مقاومت ضد حریق جنس CU2/MGT/XLPE/ISCR/OSCR/LSFOH/SWA/LSFOH تیپ JE-2X(ST)BH-TIMF سطح مقطع 12X3x1.5  -  Cable Application Instrument and Control Voltage Type Instrument and Control Voltage Level 300/500 Color Black with Red stripe Shield Shielded Armored Armand Resistance fire proof Material CU2/MGT/XLPE/ISCR/OSCR/LSFOH/SWA/LSFOH Type JE-2X(ST)BH-TIMF Surfac DRUM NO : 9710160200";

    [Fact]
    public async Task Real_reported_508_char_description_persists_exactly_on_real_sql_server_after_migration()
    {
        Assert.Equal(508, RealReportedDescription.Length);

        var uniqueCode = "REALSQLDESC-" + Guid.NewGuid().ToString("N")[..8];

        // PEMS_TEST_MRSDESC_REALSQL بین اجراها Drop نمی‌شود (رفتار مستند CreateIsolatedSqlServerOrThrow)
        // - GUID روی کد کافی نبود: چون خودِ متن Description ثابت است، دومین اجرا با MaterialItem
        // باقی‌مانده از اجرای قبلی که دقیقاً همین شرح را دارد Fuzzy-Match می‌خورد و MatchStatus
        // "MatchedExisting" می‌شود نه "New" - یعنی CommitByStatusAsync(..,"New",..) چیزی برای Commit
        // پیدا نمی‌کند (نه باگ رفع‌شده، یک آلودگی داده‌ی تست). پس دیتابیس ایزوله در ابتدای هر اجرا
        // کاملاً EnsureDeleted+Migrate می‌شود - دقیقاً همان الگوی خودتمیزکننده‌ای که قبلاً برای
        // VDRPagedListTests در همین ممیزی جاری اتخاذ شد.
        await using var ctx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow("PEMS_TEST_MRSDESC_REALSQL");
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.MigrateAsync();
        var svc = new MrsImportService(ctx, new VendorService(ctx));

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        ws.Cell(1, 1).Value = "MRS No"; ws.Cell(1, 2).Value = "Item Code"; ws.Cell(1, 3).Value = "Description"; ws.Cell(1, 4).Value = "Unit"; ws.Cell(1, 5).Value = "Qty Received";
        ws.Cell(2, 1).Value = $"WH-A-{uniqueCode}";
        ws.Cell(2, 2).Value = uniqueCode;
        ws.Cell(2, 3).Value = RealReportedDescription;
        ws.Cell(2, 4).Value = "EA";
        ws.Cell(2, 5).Value = "1";
        var ms = new MemoryStream(); wb.SaveAs(ms); ms.Position = 0;

        var (stageOk, stageErr, batchId) = await svc.StageFromExcelAsync(ms, "realsql.xlsx", null, null);
        Assert.True(stageOk, stageErr);

        // این دقیقاً همان مسیر Exception اصلی است: CommitByStatusAsync → CommitCoreAsync →
        // SaveChangesAsync روی SQL Server واقعی. قبل از رفع باگ، این‌جا SqlException پرتاب می‌شد.
        var (commitOk, commitErr, committed, skipped) = await svc.CommitByStatusAsync(batchId!.Value, "New", null);
        Assert.True(commitOk, commitErr);
        Assert.Equal(1, committed);
        Assert.Equal(0, skipped);

        // بازخوانی مستقیم و مستقل از ChangeTracker - یک Context تازه، دقیقاً مثل این‌که کاربر دیگری
        // صفحه را باز کند.
        await using var freshCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow("PEMS_TEST_MRSDESC_REALSQL");
        var persisted = await freshCtx.MaterialItems.AsNoTracking().SingleAsync(x => x.MaterialCode == uniqueCode);
        Assert.Equal(RealReportedDescription, persisted.Description);
        Assert.Equal(508, persisted.Description.Length);
    }
}
