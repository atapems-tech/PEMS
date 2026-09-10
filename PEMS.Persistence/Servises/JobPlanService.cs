using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
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

    // طبق ممیزی مستقل فاز ۶B Wave B1 (P1-01): قبلاً کل JobPlanهای فعال + کل MaintenanceTasks +
    // کل MaintenancePlans به‌صورت جداگانه در حافظه بارگذاری می‌شدند (GroupBy→Dictionary→
    // lookup در حلقه‌ی foreach) - بدون Skip/Take. حالا TaskCount/PlanUsageCount با Correlated
    // Subquery داخل همون Select محاسبه می‌شوند - در SQL اجرا می‌شوند، نه در حافظه.
    public async Task<PagedResult<JobPlanListDto>> GetListAsync(int page = 1, int pageSize = 50)
    {
        var query = _context.JobPlans.Where(x => x.IsActive);

        var totalCount = await query.CountAsync();

        page = page < 1 ? 1 : page;
        pageSize = DataGridPageSize.Normalize(pageSize);

        var items = await query
            .OrderBy(x => x.JobPlanNumber)
            .ThenBy(x => x.JobPlanId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new JobPlanListDto
            {
                JobPlanId = x.JobPlanId,
                JobPlanNumber = x.JobPlanNumber,
                Title = x.Title,
                EstimatedDurationHours = x.EstimatedDurationHours,
                IsActive = x.IsActive,
                TaskCount = _context.MaintenanceTasks.Count(t => t.JobPlanId == x.JobPlanId && t.IsActive),
                PlanUsageCount = _context.MaintenancePlans.Count(p => p.JobPlanId == x.JobPlanId && p.IsActive),
                EquipmentClassId = x.EquipmentClassId,
                EquipmentClassName = x.EquipmentClass != null ? x.EquipmentClass.Name : null,
                PermitRequired = x.PermitRequired,
                LOTORequired = x.LOTORequired
            })
            .ToListAsync();

        return new PagedResult<JobPlanListDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
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
        var jobPlan = await _context.JobPlans.Include(x => x.EquipmentClass).FirstOrDefaultAsync(x => x.JobPlanId == jobPlanId);
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
            EquipmentClassId = jobPlan.EquipmentClassId,
            EquipmentClassName = jobPlan.EquipmentClass?.Name,
            PermitRequired = jobPlan.PermitRequired,
            LOTORequired = jobPlan.LOTORequired,
            Tasks = tasks,
            RequiredSkills = requiredSkills,
            RequiredMaterials = requiredMaterials
        };
    }

    public async Task<(bool Success, string? Error, int? Id)> CreateAsync(
        string title, string? scope, decimal? estimatedDurationHours, string? safetyRequirements, string? currentUserName,
        int? equipmentClassId = null, bool permitRequired = false, bool lotoRequired = false)
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
            EquipmentClassId = equipmentClassId,
            // طبق «PEMS Phase 7.5» بند ۱۵: این دو فیلد از فاز ۴ در Entity موجود بودند اما تا این
            // فاز هیچ فرم UI ای آن‌ها را تنظیم نمی‌کرد - حالا انتخاب آگاهانه‌ی سازنده‌ی JobPlan است.
            PermitRequired = permitRequired,
            LOTORequired = lotoRequired,
            IsActive = true,
            CreatedBy = currentUserName,
            CreatedDate = DateTime.Now
        };

        await _context.JobPlans.AddAsync(jobPlan);
        await _context.SaveChangesAsync();

        return (true, null, jobPlan.JobPlanId);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(
        int jobPlanId, string title, string? scope, decimal? estimatedDurationHours, string? safetyRequirements, string? currentUserName,
        int? equipmentClassId = null, bool permitRequired = false, bool lotoRequired = false)
    {
        var jobPlan = await _context.JobPlans.FirstOrDefaultAsync(x => x.JobPlanId == jobPlanId);
        if (jobPlan == null) return (false, "Job Plan پیدا نشد.");

        title = title?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(title)) return (false, "عنوان Job Plan الزامی است.");

        jobPlan.Title = title;
        jobPlan.Scope = scope;
        jobPlan.EstimatedDurationHours = estimatedDurationHours;
        jobPlan.SafetyRequirements = safetyRequirements;
        jobPlan.EquipmentClassId = equipmentClassId;
        jobPlan.PermitRequired = permitRequired;
        jobPlan.LOTORequired = lotoRequired;
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
