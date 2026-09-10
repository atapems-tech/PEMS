using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق CMMS Production Baseline v1 بند ۲۵: هر عملیات دامنه‌ی اصلی باید یک Application Service
/// داشته باشد. Controller فقط باید نازک باشد.
/// </summary>
public interface IJobPlanService
{
    Task<PagedResult<JobPlanListDto>> GetListAsync(int page = 1, int pageSize = 50);

    Task<List<(int JobPlanId, string JobPlanNumber, string Title)>> GetPickerListAsync();

    Task<JobPlanDetailDto?> GetDetailsAsync(int jobPlanId);

    Task<(bool Success, string? Error, int? Id)> CreateAsync(
        string title, string? scope, decimal? estimatedDurationHours, string? safetyRequirements, string? currentUserName,
        int? equipmentClassId = null, bool permitRequired = false, bool lotoRequired = false);

    Task<(bool Success, string? Error)> UpdateAsync(
        int jobPlanId, string title, string? scope, decimal? estimatedDurationHours, string? safetyRequirements, string? currentUserName,
        int? equipmentClassId = null, bool permitRequired = false, bool lotoRequired = false);

    Task<(bool Success, string? Error)> AddTaskAsync(int jobPlanId, int taskNumber, string title, string? description, int? estimatedDurationMinutes);
    Task RemoveTaskAsync(int maintenanceTaskId);

    Task<(bool Success, string? Error)> AddRequiredSkillAsync(int jobPlanId, int skillId, string? minimumProficiencyLevel, decimal? plannedHours);
    Task RemoveRequiredSkillAsync(int jobPlanRequiredSkillId);

    Task<(bool Success, string? Error)> AddRequiredMaterialAsync(int jobPlanId, int materialItemId, decimal requiredQuantity);
    Task RemoveRequiredMaterialAsync(int jobPlanRequiredMaterialId);
}
