using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// شماره‌گذاری نامه با قالب <c>[DEPT]-[YEAR]-[SEQ]</c> (مثلاً ENG-2026-00123) برای همه‌ی انواع نامه. DEPT کد دپارتمان ثبت‌کننده
/// است (در نبود دپارتمان: GEN). شمارنده‌ی هر (دپارتمان، سال) در LetterNumberSequence نگه‌داری می‌شود و با RowVersion افزایش می‌یابد:
/// دو ثبت هم‌زمان هرگز یک شماره نمی‌گیرند (در تعارض، ردیف شمارنده دوباره خوانده و افزایش داده می‌شود). قبلاً شماره = «تعداد نامه‌های
/// همان نوع و سال + ۱» بود که هم‌زمانی را تحمل نمی‌کرد و شمارنده‌ی هر دپارتمان نداشت.
/// </summary>
public class LetterNumberingService : ILetterNumberingService
{
    private const int MaxAttempts = 8;

    private readonly PEMSDbContext _context;

    public LetterNumberingService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextNumberAsync(string letterType, int? projectId, int? departmentId = null)
    {
        var scope = await ResolveScopeAsync(departmentId);
        var year = DateTime.Now.Year;

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var sequence = await _context.LetterNumberSequences.FirstOrDefaultAsync(s => s.Scope == scope && s.Year == year);
            if (sequence == null)
            {
                // اولین بار برای این (دپارتمان، سال): از بزرگ‌ترین شماره‌ی موجود با همین الگو ادامه می‌دهد، نه از صفر
                sequence = new LetterNumberSequence { Scope = scope, Year = year, LastValue = await ExistingMaxAsync(scope, year) };
                _context.LetterNumberSequences.Add(sequence);
            }

            sequence.LastValue++;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // تعارض هم‌زمانی (RowVersion) یا ساخت هم‌زمان اولین ردیف: فقط ردیف شمارنده را رها می‌کند (نه کل ChangeTracker که تغییرات
                // در انتظار فراخواننده را هم می‌برد) و دوباره می‌خواند.
                _context.Entry(sequence).State = EntityState.Detached;
                continue;
            }

            var candidate = Format(scope, year, sequence.LastValue);

            // شماره‌ی دستی (مجوز ManualNumber) یا داده‌ی قدیمی ممکن است همین شماره را داشته باشد؛ به شماره‌ی بعدی می‌رود
            if (!await _context.Letters.AnyAsync(l => l.LetterNumber == candidate))
                return candidate;
        }

        throw new InvalidOperationException("Could not allocate a unique letter number; please retry.");
    }

    public static string Format(string scope, int year, int sequence) => $"{scope}-{year}-{sequence:D5}";

    /// <summary>کد دپارتمان تمیزشده برای پیشوند شماره: فقط حروف/ارقام لاتین بزرگ، حداکثر ۱۰ نویسه؛ خالی → GEN.</summary>
    public static string SanitizeScope(string? code)
    {
        var cleaned = Regex.Replace((code ?? "").ToUpperInvariant(), "[^A-Z0-9]", "");
        if (cleaned.Length > 10) cleaned = cleaned[..10];
        return cleaned.Length == 0 ? "GEN" : cleaned;
    }

    private async Task<string> ResolveScopeAsync(int? departmentId)
    {
        if (!departmentId.HasValue) return "GEN";
        var code = await _context.Departments.Where(d => d.DepartmentId == departmentId.Value).Select(d => d.Code).FirstOrDefaultAsync();
        return SanitizeScope(code);
    }

    private async Task<int> ExistingMaxAsync(string scope, int year)
    {
        var prefix = $"{scope}-{year}-";
        var numbers = await _context.Letters.Where(l => l.LetterNumber.StartsWith(prefix)).Select(l => l.LetterNumber).ToListAsync();

        var max = 0;
        foreach (var n in numbers)
            if (int.TryParse(n[prefix.Length..], out var v) && v > max) max = v;
        return max;
    }
}
