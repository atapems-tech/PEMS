using ClosedXML.Excel;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۹/۱۱:
/// همون الگوی MaintenanceHistoryImportServiceTests - Parsing، تطبیق قطعی (EquipmentTag+PointTag)،
/// Idempotency (PointId+ReadingDate دقیق)، Commit، و یکپارچگی با منطق ارزیابی چندسطحی فاز ۵.
/// </summary>
public class ConditionReadingImportServiceTests
{
    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"CrImport_{dbName}");

    private static Stream BuildWorkbook(params (string EquipmentTag, string PointTag, string Date, string Value, string? Source)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(1, 1).Value = "Equipment Tag";
        sheet.Cell(1, 2).Value = "Point Tag";
        sheet.Cell(1, 3).Value = "Reading Date";
        sheet.Cell(1, 4).Value = "Value";
        sheet.Cell(1, 5).Value = "Source";

        var r = 2;
        foreach (var (eqTag, ptTag, date, value, source) in rows)
        {
            sheet.Cell(r, 1).Value = eqTag;
            sheet.Cell(r, 2).Value = ptTag;
            sheet.Cell(r, 3).Value = date;
            sheet.Cell(r, 4).Value = value;
            sheet.Cell(r, 5).Value = source ?? "";
            r++;
        }

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static async Task<ConditionMonitoringPoint> SeedPointAsync(PEMS.Persistence.Context.PEMSDbContext ctx, string equipmentTag, string pointTag, int? evaluationProfileId = null)
    {
        var eq = new Equipment { TagNumber = equipmentTag, Name = equipmentTag, EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(eq);
        await ctx.SaveChangesAsync();

        var point = new ConditionMonitoringPoint
        {
            EquipmentId = eq.EquipmentId,
            VariableType = "Vibration",
            Tag = pointTag,
            IsActive = true,
            EvaluationProfileId = evaluationProfileId
        };
        ctx.ConditionMonitoringPoints.Add(point);
        await ctx.SaveChangesAsync();
        return point;
    }

    [Fact]
    public async Task StageFromExcelAsync_ValidInput_StagesAsNew()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_ValidInput_StagesAsNew));
        var point = await SeedPointAsync(ctx, "P-1001", "VIB-01");
        var service = new ConditionReadingImportService(ctx);

        using var stream = BuildWorkbook(("P-1001", "VIB-01", "2026-01-15T09:00:00", "3.5", "Portable"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        var rows = ctx.ConditionReadingImportRows.Where(x => x.ConditionReadingImportBatchId == batchId).ToList();
        var row = Assert.Single(rows);
        Assert.Equal("New", row.Status);
        Assert.True(row.IncludeInCommit);
        Assert.Equal(point.ConditionMonitoringPointId, row.ResolvedConditionMonitoringPointId);
    }

    [Fact]
    public async Task StageFromExcelAsync_UnknownPointTag_FlagsUnresolvedPoint()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_UnknownPointTag_FlagsUnresolvedPoint));
        await SeedPointAsync(ctx, "P-1001", "VIB-01");
        var service = new ConditionReadingImportService(ctx);

        using var stream = BuildWorkbook(("P-1001", "NO-SUCH-POINT", "2026-01-15T09:00:00", "3.5", null));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        var row = Assert.Single(ctx.ConditionReadingImportRows.Where(x => x.ConditionReadingImportBatchId == batchId));
        Assert.Equal("UnresolvedPoint", row.Status);
        Assert.False(row.IncludeInCommit);
    }

    [Fact]
    public async Task StageFromExcelAsync_MissingValue_FlagsValidationError()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_MissingValue_FlagsValidationError));
        await SeedPointAsync(ctx, "P-1001", "VIB-01");
        var service = new ConditionReadingImportService(ctx);

        using var stream = BuildWorkbook(("P-1001", "VIB-01", "2026-01-15T09:00:00", "", null));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        var row = Assert.Single(ctx.ConditionReadingImportRows.Where(x => x.ConditionReadingImportBatchId == batchId));
        Assert.Equal("ValidationError", row.Status);
        Assert.False(row.IncludeInCommit);
    }

    [Fact]
    public async Task StageFromExcelAsync_ExistingReadingAtExactSameDateTime_FlagsDuplicate()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_ExistingReadingAtExactSameDateTime_FlagsDuplicate));
        var point = await SeedPointAsync(ctx, "P-1001", "VIB-01");
        var existingDate = new DateTime(2026, 1, 15, 9, 0, 0);
        ctx.ConditionReadings.Add(new ConditionReading { ConditionMonitoringPointId = point.ConditionMonitoringPointId, ReadingDate = existingDate, Value = 2.0m });
        await ctx.SaveChangesAsync();

        var service = new ConditionReadingImportService(ctx);
        using var stream = BuildWorkbook(("P-1001", "VIB-01", "2026-01-15T09:00:00", "3.5", null));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        var row = Assert.Single(ctx.ConditionReadingImportRows.Where(x => x.ConditionReadingImportBatchId == batchId));
        Assert.Equal("Duplicate", row.Status);
        Assert.False(row.IncludeInCommit);
    }

    [Fact]
    public async Task StageFromExcelAsync_DifferentTimeOnSameDate_IsNotTreatedAsDuplicate()
    {
        // طبق طراحی: کلید Idempotency تاریخ+ساعت دقیق است - چون یک نقطه می‌تواند چند قرائت
        // واقعی در یک روز داشته باشد (SCADA)، نه فقط تاریخ خالی.
        var ctx = CreateContext(nameof(StageFromExcelAsync_DifferentTimeOnSameDate_IsNotTreatedAsDuplicate));
        var point = await SeedPointAsync(ctx, "P-1001", "VIB-01");
        ctx.ConditionReadings.Add(new ConditionReading { ConditionMonitoringPointId = point.ConditionMonitoringPointId, ReadingDate = new DateTime(2026, 1, 15, 9, 0, 0), Value = 2.0m });
        await ctx.SaveChangesAsync();

        var service = new ConditionReadingImportService(ctx);
        using var stream = BuildWorkbook(("P-1001", "VIB-01", "2026-01-15T14:00:00", "3.5", null));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        var row = Assert.Single(ctx.ConditionReadingImportRows.Where(x => x.ConditionReadingImportBatchId == batchId));
        Assert.Equal("New", row.Status);
    }

    [Fact]
    public async Task CommitBatchAsync_ApprovedNewRow_CreatesRealConditionReading()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_ApprovedNewRow_CreatesRealConditionReading));
        var point = await SeedPointAsync(ctx, "P-1001", "VIB-01");
        var service = new ConditionReadingImportService(ctx);

        using var stream = BuildWorkbook(("P-1001", "VIB-01", "2026-01-15T09:00:00", "3.5", "Portable"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        var result = await service.CommitBatchAsync(batchId);

        Assert.Equal(1, result.Created);
        var reading = Assert.Single(ctx.ConditionReadings.Where(x => x.ConditionMonitoringPointId == point.ConditionMonitoringPointId));
        Assert.Equal(3.5m, reading.Value);
        Assert.Equal("Portable", reading.Source);
        Assert.Equal("OBSERVED", reading.Provenance);
    }

    [Fact]
    public async Task CommitBatchAsync_RowExcludedByUser_IsSkippedAndNotCreated()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_RowExcludedByUser_IsSkippedAndNotCreated));
        await SeedPointAsync(ctx, "P-1001", "VIB-01");
        var service = new ConditionReadingImportService(ctx);

        using var stream = BuildWorkbook(("P-1001", "VIB-01", "2026-01-15T09:00:00", "3.5", null));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var row = ctx.ConditionReadingImportRows.Single(x => x.ConditionReadingImportBatchId == batchId);

        await service.SetRowIncludeAsync(row.ConditionReadingImportRowId, false);
        var result = await service.CommitBatchAsync(batchId);

        Assert.Equal(0, result.Created);
        Assert.Empty(ctx.ConditionReadings);
    }

    [Fact]
    public async Task SetRowIncludeAsync_CannotApproveAnUnresolvedRow()
    {
        // محافظت دفاعی: هرگز نمی‌توان یک ردیف UnresolvedPoint/ConflictPoint/ValidationError را
        // با روشن‌کردن پرچم IncludeInCommit به Commit اضافه کرد.
        var ctx = CreateContext(nameof(SetRowIncludeAsync_CannotApproveAnUnresolvedRow));
        await SeedPointAsync(ctx, "P-1001", "VIB-01");
        var service = new ConditionReadingImportService(ctx);

        using var stream = BuildWorkbook(("P-1001", "NO-SUCH-POINT", "2026-01-15T09:00:00", "3.5", null));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var row = ctx.ConditionReadingImportRows.Single(x => x.ConditionReadingImportBatchId == batchId);
        Assert.False(row.IncludeInCommit);

        await service.SetRowIncludeAsync(row.ConditionReadingImportRowId, true);

        var refreshed = ctx.ConditionReadingImportRows.Single(x => x.ConditionReadingImportRowId == row.ConditionReadingImportRowId);
        Assert.False(refreshed.IncludeInCommit);
    }

    [Fact]
    public async Task CommitBatchAsync_PointHasEvaluationProfile_EvaluatesAndFreezesVersionAtCommitTime()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_PointHasEvaluationProfile_EvaluatesAndFreezesVersionAtCommitTime));

        var profile = new EvaluationProfile
        {
            Code = "VIB-TEST", Name = "Test", VariableType = "Vibration", Version = 3,
            EffectiveFrom = DateTime.Now.AddDays(-10), IsActive = true,
            Limits = new List<EvaluationLimit>
            {
                new() { Status = "Alarm", LowValue = 4.5m, LowInclusive = true, IsActive = true }
            }
        };
        ctx.EvaluationProfiles.Add(profile);
        await ctx.SaveChangesAsync();

        var point = await SeedPointAsync(ctx, "P-1001", "VIB-01", profile.EvaluationProfileId);
        var service = new ConditionReadingImportService(ctx);

        using var stream = BuildWorkbook(("P-1001", "VIB-01", "2026-01-15T09:00:00", "6.0", null));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        await service.CommitBatchAsync(batchId);

        var reading = Assert.Single(ctx.ConditionReadings.Where(x => x.ConditionMonitoringPointId == point.ConditionMonitoringPointId));
        Assert.Equal("Alarm", reading.EvaluationStatus);
        Assert.Equal(profile.EvaluationProfileId, reading.EvaluationProfileId);
        Assert.Equal(3, reading.EvaluationProfileVersion);
    }

    [Fact]
    public async Task CommitBatchAsync_PointWithNoProfile_StaysNotEvaluated_NeverGuessesNormal()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_PointWithNoProfile_StaysNotEvaluated_NeverGuessesNormal));
        var point = await SeedPointAsync(ctx, "P-1001", "VIB-01");
        var service = new ConditionReadingImportService(ctx);

        using var stream = BuildWorkbook(("P-1001", "VIB-01", "2026-01-15T09:00:00", "99", null));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        await service.CommitBatchAsync(batchId);

        var reading = Assert.Single(ctx.ConditionReadings.Where(x => x.ConditionMonitoringPointId == point.ConditionMonitoringPointId));
        Assert.Equal("NotEvaluated", reading.EvaluationStatus);
        Assert.Null(reading.EvaluationProfileId);
    }

    [Fact]
    public async Task DiscardBatchAsync_RemovesBatchAndRows()
    {
        var ctx = CreateContext(nameof(DiscardBatchAsync_RemovesBatchAndRows));
        await SeedPointAsync(ctx, "P-1001", "VIB-01");
        var service = new ConditionReadingImportService(ctx);

        using var stream = BuildWorkbook(("P-1001", "VIB-01", "2026-01-15T09:00:00", "3.5", null));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        await service.DiscardBatchAsync(batchId);

        Assert.Null(await service.GetBatchSummaryAsync(batchId));
        Assert.Empty(ctx.ConditionReadingImportRows.Where(x => x.ConditionReadingImportBatchId == batchId));
    }

    [Fact]
    public async Task GetBatchAsync_UnknownBatch_ReturnsNull()
    {
        var ctx = CreateContext(nameof(GetBatchAsync_UnknownBatch_ReturnsNull));
        var service = new ConditionReadingImportService(ctx);

        Assert.Null(await service.GetBatchAsync(999, 1, 50, null));
    }
}
