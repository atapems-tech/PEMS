using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B Wave B1» بند ۲۴ (SQL Verification): شواهد SQL واقعی (ToQueryString) روی
/// PEMSDB واقعی توسعه. مهم‌تر از وجود OFFSET/FETCH این است که خودِ Expression اولویت
/// (Alert/Overdue/DueSoon/سایر - یک CASE WHEN تودرتو) واقعاً توسط SQL Server ترجمه می‌شود؛ اگر
/// قابل ترجمه نبود، ToQueryString/اجرای واقعی همین‌جا با خطا شکست می‌خورد، نه اینکه بی‌صدا به
/// ارزیابی سمت-کلاینت (Client Evaluation) سقوط کند.
/// </summary>
public class InspectionGridLiveDbTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public InspectionGridLiveDbTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetPagedFleetAsync_AgainstRealPEMSDB_GeneratesOffsetFetchSql()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();

        var query = ctx.InspectionRecords
            .Where(x => x.IsActive)
            .OrderBy(x =>
                (x.MeasurementValue.HasValue &&
                    ((x.LowerLimit.HasValue && x.MeasurementValue.Value < x.LowerLimit.Value) ||
                     (x.UpperLimit.HasValue && x.MeasurementValue.Value > x.UpperLimit.Value)))
                    ? 0
                : (x.NextInspectionDate.HasValue && x.NextInspectionDate.Value.Date < DateTime.Today)
                    ? 1
                : (x.NextInspectionDate.HasValue && x.NextInspectionDate.Value.Date <= DateTime.Today.AddDays(30))
                    ? 2
                    : 3)
            .ThenByDescending(x => x.InspectionDate)
            .ThenByDescending(x => x.InspectionRecordId)
            .Skip(0)
            .Take(20)
            .Select(x => new { x.InspectionRecordId, TagNumber = x.Equipment != null ? x.Equipment.TagNumber : string.Empty });

        var sql = query.ToQueryString();
        _output.WriteLine(sql);

        Assert.Contains("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FETCH", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CASE", sql, StringComparison.OrdinalIgnoreCase);

        var totalInDb = await ctx.InspectionRecords.CountAsync(x => x.IsActive);

        var service = new InspectionService(ctx);
        var result = await service.GetPagedFleetAsync(page: 1, pageSize: 20);

        Assert.Equal(totalInDb, result.TotalCount);
        Assert.True(result.Items.Count <= 20);
    }
}
