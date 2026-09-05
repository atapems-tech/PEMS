using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق CMMS Production Baseline v1 بند P0-04: NumberingService یک شمارنده‌ی تازه از صفر برای
/// هر (DocumentType, ScopeKey) می‌سازد - اگر رکوردهای قدیمی (از قبل از این سرویس، با الگوی
/// Count()+1) از قبل همان شماره‌ها را گرفته باشند، اولین شماره‌ی جدید با آن‌ها برخورد می‌کند
/// (مثلاً WO-2026-0001 که قبلاً واقعاً استفاده شده). این Seeder یک‌بار، فقط اگر هنوز
/// NumberingSequence ای برای آن Scope نساخته شده، آن را از روی بیشینه‌ی شماره‌ی موجود Seed
/// می‌کند - هرگز یک شمارنده‌ی از قبل موجود را دوباره نمی‌نویسد.
/// </summary>
public static class NumberingBackfillSeeder
{
    private static readonly Regex WorkOrderPattern = new(@"^WO-(\d{4})-(\d+)$");

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var existingNumbers = await db.WorkOrders.Select(x => x.WorkOrderNumber).ToListAsync();

        var maxByYear = new Dictionary<string, int>();
        foreach (var number in existingNumbers)
        {
            var match = WorkOrderPattern.Match(number ?? "");
            if (!match.Success) continue;

            var year = match.Groups[1].Value;
            var sequence = int.Parse(match.Groups[2].Value);

            if (!maxByYear.TryGetValue(year, out var current) || sequence > current)
                maxByYear[year] = sequence;
        }

        foreach (var (year, maxSequence) in maxByYear)
        {
            var alreadySeeded = await db.NumberingSequences
                .AnyAsync(x => x.DocumentType == "WorkOrder" && x.ScopeKey == year);

            if (alreadySeeded) continue;

            db.NumberingSequences.Add(new NumberingSequence
            {
                DocumentType = "WorkOrder",
                ScopeKey = year,
                CurrentValue = maxSequence,
                Prefix = "WO",
                PadWidth = 4,
                IncludeScopeInFormat = true,
                CreatedDate = DateTime.Now
            });
        }

        await db.SaveChangesAsync();
    }
}
