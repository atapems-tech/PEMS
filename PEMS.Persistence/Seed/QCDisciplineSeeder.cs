using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// دیسیپلین‌های استاندارد QC/QA طبق رویه‌ی واقعی صنعت پتروشیمی/EPC - جدا از دیسیپلین‌های
/// مهندسی طراحی (Discipline). این دیسیپلین‌ها بر اساس روش/حوزه‌ی بازرسی تعریف می‌شوند.
/// </summary>
public static class QCDisciplineSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        if (await db.QCDisciplines.AnyAsync()) return;

        var defaults = new[]
        {
            ("NDT", "Non-Destructive Testing (RT/UT/PT/MT)"),
            ("WLD", "Welding Inspection"),
            ("COAT", "Coating & Painting Inspection"),
            ("DIM", "Dimensional & Mechanical Inspection"),
            ("EI", "Electrical & Instrument Testing"),
            ("CIV", "Civil & Structural QC"),
            ("HSE", "HSE / Safety Inspection"),
            ("MAT", "Material & Receiving Inspection")
        };

        foreach (var (code, name) in defaults)
        {
            db.QCDisciplines.Add(new QCDiscipline
            {
                Code = code,
                Name = name,
                IsActive = true,
                CreatedDate = DateTime.Now
            });
        }

        await db.SaveChangesAsync();
    }
}
