using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class JobPlanService : IJobPlanService
{
    private readonly PEMSDbContext _context;
    private readonly INumberingService _numberingService;

    public JobPlanService(PEMSDbContext context, INumberingService numberingService)
    {
        _context = context;
        _numberingService = numberingService;
    }

    public async Task<List<JobPlanListDto>> GetListAsync()
    {
        var taskCounts = await _context.MaintenanceTasks.Where(x => x.IsActive)
            .GroupBy(x => x.JobPlanId)
            .Select(g => new { JobPlanId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.JobPlanId, x => x.Count);

        var usageCounts = await _context.MaintenancePlans.Where(x => x.IsActive && x.JobPlanId != null)
            .GroupBy(x => x.JobPlanId!.Value)
            .Select(g => new { JobPlanId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.JobPlanId, x => x.Count);

        var jobPlans = await _context.JobPlans
            .Where(x => x.IsActive)
            .OrderBy(x => x.JobPlanNumber)
            .Select(x => new JobPlanListDto
            {
                JobPlanId = x.JobPlanId,
                JobPlanNumber = x.JobPlanNumber,
                Title = x.Title,
                EstimatedDurationHours = x.EstimatedDurationHours,
                IsActive = x.IsActive
            })
            .ToListAsync();

        foreach (var dto in jobPlans)
        {
            dto.TaskCount = taskCounts.GetValueOrDefault(dto.JobPlanId);
            dto.PlanUsageCount = usageCounts.GetValueOrDefault(dto.JobPlanId);
        }

        return jobPlans;
    }

    public async Task<List<(int JobPlanId, string JobPlanNumber, string Title)>> GetPickerListAsync()
    {
        return await _context.JobPlans.Where(x => x.IsActive)
            .OrderBy(x => x.JobPlanNumber)
            .Select(x => new ValueTuple<int, string, string>(x.JobPlanId, x.JobPlanNumber, x.Title))
            .ToListAsync();
    }

    public async Task<JobPlanDetailDto?> GetDetailsAsync(int jobPlanId)
    {
        var jobPlan = await _context.JobPlans.FirstOrDefaultAsync(x => x.JobPlanId == jobPlanId);
        if (jobPlan == null) return null;

        var tasks = await _context.MaintenanceTasks
            .Where(x => x.JobPlanId == jobPlanId && x.IsActive)
            .OrderBy(x => x.TaskNumber)
            .Select(x => new MaintenanceTaskDto
            {
                MaintenanceTaskId = x.MaintenanceTaskId,
                TaskNumber = x.TaskNumber,
                Title = x.Title,
                Description = x.Description,
                EstimatedDurationMinutes = x.EstimatedDurationMinutes
            })
            .ToListAsync();

        var requiredSkills = await _context.JobPlanRequiredSkills
            .Include(x => x.Skill)
            .Where(x => x.JobPlanId == jobPlanId)
            .Select(x => new JobPlanRequiredSkillDto
            {
                JobPlanRequiredSkillId = x.JobPlanRequiredSkillId,
                SkillId = x.SkillId,
                SkillName = x.Skill!.Name,
                MinimumProficiencyLevel = x.MinimumProficiencyLevel,
                PlannedHours = x.PlannedHours
            })
            .ToListAsync();

        var requiredMaterials = await _context.JobPlanRequiredMaterials
            .Include(x => x.MaterialItem)
            .Where(x => x.JobPlanId == jobPlanId)
            .Select(x => new JobPlanRequiredMaterialDto
            {
                JobPlanRequiredMaterialId = x.JobPlanRequiredMaterialId,
                MaterialItemId = x.MaterialItemId,
                MaterialCode = x.MaterialItem!.MaterialCode,
                MaterialDescription = x.MaterialItem.Description,
                RequiredQuantity = x.RequiredQuantity
            })
            .ToListAsync();

        return new JobPlanDetailDto
        {
            JobPlanId = jobPlan.JobPlanId,
            JobPlanNumber = jobPlan.JobPlanNumber,
            Title = jobPlan.Title,
            Scope = jobPlan.Scope,
            EstimatedDurationHours = jobPlan.EstimatedDurationHours,
            SafetyRequirements = jobPlan.SafetyRequirements,
            IsActive = jobPlan.IsActive,
            Tasks = tasks,
            RequiredSkills = requiredSkills,
            RequiredMaterials = requiredMaterials
        };
    }

    public async Task<(bool Success, string? Error, int? Id)> CreateAsync(
        string title, string? scope, decimal? estimatedDurationHours, string? safetyRequirements, string? currentUserName)
    {
        title = title?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(title)) return (false, "عنوان Job Plan الزامی است.", null);

        var jobPlan = new JobPlan
        {
            JobPlanNumber = await _numberingService.GetNextNumberAsync("JobPlan"),
            Title = title,
            Scope = scope,
            EstimatedDurationHours = estimatedDurationHours,
            SafetyRequirements = safetyRequirements,
            IsActive = true,
            CreatedBy = currentUserName,
            CreatedDate = DateTime.Now
        };

        await _context.JobPlans.AddAsync(jobPlan);
        await _context.SaveChangesAsync();

        return (true, null, jobPlan.JobPlanId);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(
        int jobPlanId, string title, string? scope, decimal? estimatedDurationHours, string? safetyRequirements, string? currentUserName)
    {
        var jobPlan = await _context.JobPlans.FirstOrDefaultAsync(x => x.JobPlanId == jobPlanId);
        if (jobPlan == null) return (false, "Job Plan پیدا نشد.");

        title = title?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(title)) return (false, "عنوان Job Plan الزامی است.");

        jobPlan.Title = title;
        jobPlan.Scope = scope;
        jobPlan.EstimatedDurationHours = estimatedDurationHours;
        jobPlan.SafetyRequirements = safetyRequirements;
        jobPlan.ModifiedBy = currentUserName;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AddTaskAsync(
        int jobPlanId, int taskNumber, string title, string? description, int? estimatedDurationMinutes)
    {
        title = title?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(title)) return (false, "عنوان Task الزامی است.");

        if (await _context.MaintenanceTasks.AnyAsync(x => x.JobPlanId == jobPlanId && x.TaskNumber == taskNumber && x.IsActive))
            return (false, $"شماره Task «{taskNumber}» قبلاً استفاده شده است.");

        await _context.MaintenanceTasks.AddAsync(new MaintenanceTask
        {
            JobPlanId = jobPlanId,
            TaskNumber = taskNumber,
            Title = title,
            Description = description,
            EstimatedDurationMinutes = estimatedDurationMinutes,
            IsActive = true
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task RemoveTaskAsync(int maintenanceTaskId)
    {
        var task = await _context.MaintenanceTasks.FirstOrDefaultAsync(x => x.MaintenanceTaskId == maintenanceTaskId);
        if (task == null) return;

        task.IsActive = false;
        await _context.SaveChangesAsync();
    }

    public async Task<(bool Success, string? Error)> AddRequiredSkillAsync(int jobPlanId, int skillId, string? minimumProficiencyLevel, decimal? plannedHours)
    {
        if (await _context.JobPlanRequiredSkills.AnyAsync(x => x.JobPlanId == jobPlanId && x.SkillId == skillId))
            return (false, "این مهارت قبلاً اضافه شده.");

        await _context.JobPlanRequiredSkills.AddAsync(new JobPlanRequiredSkill
        {
            JobPlanId = jobPlanId,
            SkillId = skillId,
            MinimumProficiencyLevel = minimumProficiencyLevel,
            PlannedHours = plannedHours,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task RemoveRequiredSkillAsync(int jobPlanRequiredSkillId)
    {
        var row = await _context.JobPlanRequiredSkills.FirstOrDefaultAsync(x => x.JobPlanRequiredSkillId == jobPlanRequiredSkillId);
        if (row == null) return;

        _context.JobPlanRequiredSkills.Remove(row);
        await _context.SaveChangesAsync();
    }

    public async Task<(bool Success, string? Error)> AddRequiredMaterialAsync(int jobPlanId, int materialItemId, decimal requiredQuantity)
    {
        if (requiredQuantity <= 0) return (false, "مقدار باید بزرگ‌تر از صفر باشد.");

        if (await _context.JobPlanRequiredMaterials.AnyAsync(x => x.JobPlanId == jobPlanId && x.MaterialItemId == materialItemId))
            return (false, "این متریال قبلاً اضافه شده.");

        await _context.JobPlanRequiredMaterials.AddAsync(new JobPlanRequiredMaterial
        {
            JobPlanId = jobPlanId,
            MaterialItemId = materialItemId,
            RequiredQuantity = requiredQuantity,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task RemoveRequiredMaterialAsync(int jobPlanRequiredMaterialId)
    {
        var row = await _context.JobPlanRequiredMaterials.FirstOrDefaultAsync(x => x.JobPlanRequiredMaterialId == jobPlanRequiredMaterialId);
        if (row == null) return;

        _context.JobPlanRequiredMaterials.Remove(row);
        await _context.SaveChangesAsync();
    }
}
