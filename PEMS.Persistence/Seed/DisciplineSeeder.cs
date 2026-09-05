using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

public static class DisciplineSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        if (await db.Disciplines.AnyAsync()) return;

        var defaults = new[]
        {
            new Discipline { Code = "MECH", Name = "Mechanical", Description = "Mechanical engineering discipline", ImportAliases = "ME, MEC, MECH ENG, Mechanical Engineering", IsActive = true, CreatedDate = DateTime.Now },
            new Discipline { Code = "ELEC", Name = "Electrical", Description = "Electrical engineering discipline", ImportAliases = "EL, ELEC, E&I, Electrical Engineering", IsActive = true, CreatedDate = DateTime.Now },
            new Discipline { Code = "INST", Name = "Instrumentation", Description = "Instrumentation and controls discipline", ImportAliases = "IN, INS, I&C, Instrumentation", IsActive = true, CreatedDate = DateTime.Now },
            new Discipline { Code = "PROC", Name = "Process", Description = "Process engineering discipline", ImportAliases = "PR, PRO, PROCESS, Process Engineering", IsActive = true, CreatedDate = DateTime.Now },
            new Discipline { Code = "PIP", Name = "Piping", Description = "Piping engineering discipline", ImportAliases = "PI, PIPING, Pipe, Piping Engineering", IsActive = true, CreatedDate = DateTime.Now },
            new Discipline { Code = "CIV", Name = "Civil", Description = "Civil and structural engineering discipline", ImportAliases = "CV, CIVIL, Structural, Civil Engineering", IsActive = true, CreatedDate = DateTime.Now },
            new Discipline { Code = "STR", Name = "Structural", Description = "Structural engineering discipline", ImportAliases = "ST, STRUCT, Structural Engineering", IsActive = true, CreatedDate = DateTime.Now },
            new Discipline { Code = "HSE", Name = "HSE", Description = "Health, safety and environment discipline", ImportAliases = "HS, SHE, SAFETY, HSE Engineering", IsActive = true, CreatedDate = DateTime.Now },
            new Discipline { Code = "ARCH", Name = "Architecture", Description = "Architectural engineering discipline", ImportAliases = "AR, ARCHITECTURE, Architectural", IsActive = true, CreatedDate = DateTime.Now }
        };

        db.Disciplines.AddRange(defaults);
        await db.SaveChangesAsync();
    }
}
