using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// طبق ممیزی «MaterialItems.Description String-Length Integrity»: سقف قبلی ۵۰۰ کاراکتری روی
/// MaterialItem.Description (هم در EF - HasMaxLength(500) - و هم در واقع در دیتابیس زنده -
/// nvarchar(500)) یک محدودیت واقعی کسب‌وکار نبود؛ همون Migration اولیه (MaterialMaster) که این
/// ستون را ساخته بود، در همون لحظه Description را روی MaterialCategory/MaterialUOM بدون سقف
/// (nvarchar(max)) گذاشته بود - بدون هیچ توضیحی چرا MaterialItem.Description استثنا شد. داده‌ی
/// واقعی MRS (۱۱٬۸۳۰ ردیف Stage شده در PEMSDB) ۵ ردیف با طول بیش از ۵۰۰ (حداکثر واقعی ۵۴۰) دارد -
/// از جمله دقیقاً همان شرح کابل فارسی/انگلیسی گزارش‌شده در Exception اصلی (۵۰۸ کاراکتر، ردیف
/// MrsImportRowId=66 در Batch=1). این تست‌ها ثابت می‌کنند بعد از حذف سقف (Migration
/// WidenMaterialItemDescriptionToUnbounded → nvarchar(max)، هماهنگ با بقیه‌ی فیلدهای متنی آزاد
/// همین Entity)، این مقدار واقعی و مقادیر بزرگ‌تر/متنوع‌تر بدون هیچ Truncate/Normalize ای، دقیقاً
/// حرف‌به‌حرف Persist می‌شوند - چه در یک Import کوچک، چه دقیقاً روی مرز بلوک داخلی ۱۰۰-تایی Commit
/// (نامرئی به کاربر، مستند در MrsImportBatchCapacityTests).
/// </summary>
public class MrsImportDescriptionLengthTests
{
    /// <summary>مقدار دقیق واقعی که Exception را در تولید ایجاد کرد - از PEMSDB.dbo.MrsImportRows، MrsImportRowId=66 (۵۰۸ کاراکتر).</summary>
    private const string RealReportedDescription =
        "کابل کاربرد ابزار دقیق و کنترل نوع ولتاژ ابزار دقیق و کنترل سطح ولتاژ 300/500 رنگ مشکی با خط قرمز شیلد شیلد دار آرمورد آرمورد دار مقاومت ضد حریق جنس CU2/MGT/XLPE/ISCR/OSCR/LSFOH/SWA/LSFOH تیپ JE-2X(ST)BH-TIMF سطح مقطع 12X3x1.5  -  Cable Application Instrument and Control Voltage Type Instrument and Control Voltage Level 300/500 Color Black with Red stripe Shield Shielded Armored Armand Resistance fire proof Material CU2/MGT/XLPE/ISCR/OSCR/LSFOH/SWA/LSFOH Type JE-2X(ST)BH-TIMF Surfac DRUM NO : 9710160200";

    private static (MrsImportService svc, PEMSDbContext ctx) Seed(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory(dbName);
        return (new MrsImportService(ctx, new VendorService(ctx)), ctx);
    }

    private static MemoryStream BuildWorkbook(IReadOnlyList<(string ItemCode, string Description)> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        ws.Cell(1, 1).Value = "MRS No"; ws.Cell(1, 2).Value = "Item Code"; ws.Cell(1, 3).Value = "Description"; ws.Cell(1, 4).Value = "Unit"; ws.Cell(1, 5).Value = "Qty Received";
        for (var i = 0; i < rows.Count; i++)
        {
            var r = i + 2;
            ws.Cell(r, 1).Value = $"WH-A-{rows[i].ItemCode}";
            ws.Cell(r, 2).Value = rows[i].ItemCode;
            ws.Cell(r, 3).Value = rows[i].Description;
            ws.Cell(r, 4).Value = "EA";
            ws.Cell(r, 5).Value = "1";
        }
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    /// <summary>یک متن ترکیبی (فارسی/عربی/انگلیسی/عدد/علائم/اسلش/پرانتز) که دقیقاً به طول موردنظر می‌رسد - برای تست‌های مرزی طول دقیق، و یکتا (پسوند شماره‌دار) تا داخل یک Batch با هم DuplicateInBatch نشوند.</summary>
    private static string MakeMixedUnicodeDescription(int exactLength, string uniqueSuffix)
    {
        const string pattern = "کابل ابزار دقیق نوع 300/500 (Cable Type A) کنترل сталь ملاحظات فنی: XLPE/SWA-2X، دما ۲۵ درجه؛ Ø12mm - ";
        var sb = new System.Text.StringBuilder();
        while (sb.Length < exactLength) sb.Append(pattern);
        var suffixed = sb.ToString(0, Math.Max(0, exactLength - uniqueSuffix.Length)) + uniqueSuffix;
        return suffixed.Length == exactLength ? suffixed : suffixed[..exactLength];
    }

    // ---------------- The exact real production failure ----------------
    [Fact]
    public async Task Real_reported_persian_english_cable_description_508_chars_stages_and_commits_exactly()
    {
        Assert.Equal(508, RealReportedDescription.Length);

        var (svc, ctx) = Seed(nameof(Real_reported_persian_english_cable_description_508_chars_stages_and_commits_exactly));
        using var wb = BuildWorkbook(new[] { ("REALDESC-1", RealReportedDescription) });

        var (stageOk, stageErr, batchId) = await svc.StageFromExcelAsync(wb, "real.xlsx", null, null);
        Assert.True(stageOk, stageErr);

        var stagedRow = await ctx.MrsImportRows.SingleAsync(x => x.MrsImportBatchId == batchId);
        Assert.Equal(RealReportedDescription, stagedRow.RawDescription);
        Assert.Equal("New", stagedRow.MatchStatus);

        var (commitOk, commitErr, committed, skipped) = await svc.CommitByStatusAsync(batchId!.Value, "New", null);
        Assert.True(commitOk, commitErr);
        Assert.Equal(1, committed);
        Assert.Equal(0, skipped);

        var item = await ctx.MaterialItems.SingleAsync(x => x.MaterialCode == "REALDESC-1");
        Assert.Equal(RealReportedDescription, item.Description);
        Assert.Equal(508, item.Description.Length);
    }

    // ---------------- Boundary sizes around the old 500-char cap, and well beyond it ----------------
    [Theory]
    [InlineData(1)]
    [InlineData(499)]
    [InlineData(500)]
    [InlineData(501)]
    [InlineData(1000)]
    [InlineData(2000)]
    public async Task Description_of_exact_length_n_persists_character_for_character(int n)
    {
        var (svc, ctx) = Seed(nameof(Description_of_exact_length_n_persists_character_for_character) + n);
        var description = MakeMixedUnicodeDescription(n, "#1");
        Assert.Equal(n, description.Length);

        using var wb = BuildWorkbook(new[] { ($"LEN{n}-1", description) });
        var (stageOk, stageErr, batchId) = await svc.StageFromExcelAsync(wb, "len.xlsx", null, null);
        Assert.True(stageOk, stageErr);

        var (commitOk, commitErr, committed, _) = await svc.CommitByStatusAsync(batchId!.Value, "New", null);
        Assert.True(commitOk, commitErr);
        Assert.Equal(1, committed);

        var item = await ctx.MaterialItems.SingleAsync(x => x.MaterialCode == $"LEN{n}-1");
        Assert.Equal(description, item.Description);
        Assert.Equal(n, item.Description.Length);
    }

    // ---------------- Unicode fidelity: Persian, Arabic, English, digits, punctuation, mixed, '/', '()', engineering notation ----------------
    [Theory]
    [InlineData("فارسی خالص با نیم‌فاصله و ۰۱۲۳ اعداد فارسی")]
    [InlineData("نص عربي خالص للاختبار مع أرقام ١٢٣")]
    [InlineData("Pure English description with digits 12345 and punctuation: , . ; :")]
    [InlineData("Mixed فارسی and English در یک رشته با 300/500 و (parentheses) و Ø12mm ± 0.5")]
    [InlineData("Slash/And/Parentheses (Type A/B) — Engineering Notation Ø25×1.5mm @ 90°C")]
    [InlineData("Cable Type JE-2X(ST)BH-TIMF, CU2/MGT/XLPE/ISCR/OSCR/LSFOH/SWA/LSFOH")]
    public async Task Unicode_and_mixed_script_descriptions_persist_without_normalization_or_character_loss(string description)
    {
        var (svc, ctx) = Seed(nameof(Unicode_and_mixed_script_descriptions_persist_without_normalization_or_character_loss) + description.GetHashCode());
        using var wb = BuildWorkbook(new[] { ("UNI-1", description) });

        var (stageOk, stageErr, batchId) = await svc.StageFromExcelAsync(wb, "uni.xlsx", null, null);
        Assert.True(stageOk, stageErr);

        var (commitOk, commitErr, committed, _) = await svc.CommitByStatusAsync(batchId!.Value, "New", null);
        Assert.True(commitOk, commitErr);
        Assert.Equal(1, committed);

        var item = await ctx.MaterialItems.SingleAsync(x => x.MaterialCode == "UNI-1");
        Assert.Equal(description, item.Description);
    }

    // ---------------- Bulk / internal-chunk-boundary tests: the long row must commit correctly no matter where it falls relative to the 100-row internal chunk boundary ----------------
    [Theory]
    [InlineData(1, 5)]      // اول Batch
    [InlineData(100, 150)]  // دقیقاً آخرین ردیف بلوک داخلی اول
    [InlineData(101, 150)]  // دقیقاً اولین ردیف بلوک داخلی دوم
    [InlineData(500, 600)]  // مرز دقیق سقف قدیمی UI/صفحه‌بندی (بی‌ربط به Commit، ولی صراحتاً پوشش داده می‌شود)
    [InlineData(501, 600)]
    [InlineData(1000, 1050)]
    public async Task Long_description_commits_correctly_regardless_of_position_relative_to_internal_chunk_boundary(int longRowPosition, int totalRows)
    {
        var (svc, ctx) = Seed($"{nameof(Long_description_commits_correctly_regardless_of_position_relative_to_internal_chunk_boundary)}_{longRowPosition}_{totalRows}");

        var rows = new List<(string ItemCode, string Description)>();
        for (var i = 1; i <= totalRows; i++)
        {
            var description = i == longRowPosition
                ? RealReportedDescription
                : $"Filler Material Description {i}";
            rows.Add(($"BULK{totalRows}-{i}", description));
        }

        using var wb = BuildWorkbook(rows);
        var (stageOk, stageErr, batchId) = await svc.StageFromExcelAsync(wb, "bulk.xlsx", null, null);
        Assert.True(stageOk, stageErr);
        Assert.Equal(totalRows, await ctx.MrsImportRows.CountAsync(x => x.MrsImportBatchId == batchId));

        var (commitOk, commitErr, committed, skipped) = await svc.CommitByStatusAsync(batchId!.Value, "New", null);
        Assert.True(commitOk, commitErr);
        Assert.Equal(totalRows, committed);
        Assert.Equal(0, skipped);

        var longItem = await ctx.MaterialItems.SingleAsync(x => x.MaterialCode == $"BULK{totalRows}-{longRowPosition}");
        Assert.Equal(RealReportedDescription, longItem.Description);
        Assert.Equal(508, longItem.Description.Length);

        // بقیه‌ی ردیف‌ها هم واقعاً Commit شدند - نه این‌که فقط شمارشگر درست بود ولی خودِ ردیف گم شد
        Assert.Equal(totalRows, await ctx.MaterialItems.CountAsync(x => x.MaterialCode.StartsWith($"BULK{totalRows}-")));
    }

    // ---------------- Empty description behavior is unchanged by this fix (pre-existing "Invalid" path) ----------------
    [Fact]
    public async Task Empty_description_is_still_marked_invalid_unaffected_by_length_cap_removal()
    {
        var (svc, ctx) = Seed(nameof(Empty_description_is_still_marked_invalid_unaffected_by_length_cap_removal));
        using var wb = BuildWorkbook(new[] { ("EMPTY-1", "") });

        var (stageOk, stageErr, batchId) = await svc.StageFromExcelAsync(wb, "empty.xlsx", null, null);
        Assert.True(stageOk, stageErr);

        var row = await ctx.MrsImportRows.SingleAsync(x => x.MrsImportBatchId == batchId);
        Assert.Equal("Invalid", row.MatchStatus);
    }

    // ---------------- Sibling finding from the mandated full-mapping audit: MaterialCode is a real,
    // deliberately-bounded business identifier (unlike Description) - an oversized Item Code must
    // produce a controlled row-level "Invalid" result, not a SqlException/HTTP 500, and must never
    // be silently truncated. ----------------
    [Fact]
    public async Task Item_code_exceeding_the_genuine_100_char_MaterialCode_limit_is_rejected_as_a_controlled_row_error_not_a_crash()
    {
        var (svc, ctx) = Seed(nameof(Item_code_exceeding_the_genuine_100_char_MaterialCode_limit_is_rejected_as_a_controlled_row_error_not_a_crash));
        var oversizedCode = new string('X', 125);
        using var wb = BuildWorkbook(new[] { (oversizedCode, "Some Valid Description") });

        var (stageOk, stageErr, batchId) = await svc.StageFromExcelAsync(wb, "oversized.xlsx", null, null);
        Assert.True(stageOk, stageErr);

        var (commitOk, commitErr, committed, skipped) = await svc.CommitByStatusAsync(batchId!.Value, "New", null);
        Assert.True(commitOk, commitErr);
        Assert.Equal(0, committed);
        Assert.Equal(1, skipped);

        var row = await ctx.MrsImportRows.SingleAsync(x => x.MrsImportBatchId == batchId);
        Assert.Equal("Invalid", row.MatchStatus);
        Assert.Contains("125", row.ErrorMessage);
        Assert.Equal(oversizedCode, row.RawItemCode); // مقدار اصلی دست‌نخورده حفظ شد - نه Truncate

        Assert.Equal(0, await ctx.MaterialItems.CountAsync());
    }

    [Fact]
    public async Task Item_code_at_exactly_the_100_char_MaterialCode_limit_commits_successfully()
    {
        var (svc, ctx) = Seed(nameof(Item_code_at_exactly_the_100_char_MaterialCode_limit_commits_successfully));
        var boundaryCode = new string('Y', 100);
        using var wb = BuildWorkbook(new[] { (boundaryCode, "Some Valid Description") });

        var (stageOk, stageErr, batchId) = await svc.StageFromExcelAsync(wb, "boundary.xlsx", null, null);
        Assert.True(stageOk, stageErr);

        var (commitOk, commitErr, committed, skipped) = await svc.CommitByStatusAsync(batchId!.Value, "New", null);
        Assert.True(commitOk, commitErr);
        Assert.Equal(1, committed);
        Assert.Equal(0, skipped);

        var item = await ctx.MaterialItems.SingleAsync();
        Assert.Equal(boundaryCode, item.MaterialCode);
    }

    // ---------------- Duplicate-commit safety and vendor resolution are unaffected by the length fix ----------------
    [Fact]
    public async Task Committing_a_batch_containing_the_long_description_twice_does_not_duplicate_the_material_item()
    {
        var (svc, ctx) = Seed(nameof(Committing_a_batch_containing_the_long_description_twice_does_not_duplicate_the_material_item));
        using var wb = BuildWorkbook(new[] { ("DUPLONG-1", RealReportedDescription) });

        var (stageOk, stageErr, batchId) = await svc.StageFromExcelAsync(wb, "duplong.xlsx", null, null);
        Assert.True(stageOk, stageErr);

        var first = await svc.CommitByStatusAsync(batchId!.Value, "New", null);
        Assert.True(first.Success, first.Error);
        Assert.Equal(1, first.Committed);

        var second = await svc.CommitByStatusAsync(batchId.Value, "New", null);
        Assert.True(second.Success, second.Error);
        Assert.Equal(0, second.Committed);

        Assert.Equal(1, await ctx.MaterialItems.CountAsync(x => x.MaterialCode == "DUPLONG-1"));
    }
}
