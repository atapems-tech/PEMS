using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Seed;

public static class DepartmentSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        if (await db.Departments.AnyAsync()) return;

        var defaults = new[]
        {
            ("ENG", "Engineering"),
            ("PROC", "Procurement"),
            ("CONST", "Construction"),
            ("QAQC", "QA/QC"),
            ("HSE", "HSE"),
            ("DCC", "Document Control"),
            ("PM", "Project Management"),
            ("PLAN", "Planning & Control"),
            ("CMMS", "Maintenance"),
            ("FIN", "Finance"),
            ("HR", "Human Resources"),
            ("IT", "IT")
        };

        foreach (var (code, name) in defaults)
        {
            db.Departments.Add(new Department
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
