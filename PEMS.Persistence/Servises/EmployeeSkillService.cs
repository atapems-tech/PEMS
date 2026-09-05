using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class EmployeeSkillService : IEmployeeSkillService
{
    private readonly PEMSDbContext _context;

    public EmployeeSkillService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task AddOrReactivateAsync(int employeeId, int skillId, string proficiencyLevel, DateTime? certifiedDate)
    {
        var existing = await _context.EmployeeSkills
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.SkillId == skillId);

        if (existing != null && existing.IsActive)
            throw new Exception("این مهارت از قبل به این کارمند تخصیص داده شده.");

        if (existing != null && !existing.IsActive)
        {
            existing.IsActive = true;
            existing.ProficiencyLevel = proficiencyLevel;
            existing.CertifiedDate = certifiedDate;
            await _context.SaveChangesAsync();
            return;
        }

        await _context.EmployeeSkills.AddAsync(new EmployeeSkill
        {
            EmployeeId = employeeId,
            SkillId = skillId,
            ProficiencyLevel = proficiencyLevel,
            CertifiedDate = certifiedDate,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
    }

    public async Task RemoveAsync(int employeeSkillId)
    {
        var employeeSkill = await _context.EmployeeSkills.FirstOrDefaultAsync(x => x.EmployeeSkillId == employeeSkillId);
        if (employeeSkill == null) throw new Exception("رکورد یافت نشد.");

        employeeSkill.IsActive = false;
        await _context.SaveChangesAsync();
    }
}
