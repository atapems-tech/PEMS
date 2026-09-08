using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 2 — Maintenance Intelligence Engine» بند ۲۰: پیاده‌سازی Query-Time - همه‌ی
/// کوئری‌ها AsNoTracking و هدفمند هستند (بدون N+1؛ هر مجموعه با یک Query مستقل و فیلترشده روی
/// EquipmentClassId واکشی می‌شود، نه یک Join سنگین همه‌باهم).
/// </summary>
public class MaintenanceIntelligenceService : IMaintenanceIntelligenceService
{
    private readonly PEMSDbContext _context;

    public MaintenanceIntelligenceService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<EquipmentMaintenanceIntelligenceDto?> GetForEquipmentAsync(int equipmentId)
    {
        var equipment = await _context.Equipments
            .AsNoTracking()
            .Where(x => x.EquipmentId == equipmentId)
            .Select(x => new { x.EquipmentId, x.TagNumber, x.EquipmentType, x.EquipmentClassId })
            .FirstOrDefaultAsync();

        if (equipment == null) return null;

        var result = new EquipmentMaintenanceIntelligenceDto
        {
            EquipmentId = equipment.EquipmentId,
            TagNumber = equipment.TagNumber,
            EquipmentType = equipment.EquipmentType,
            EquipmentClassId = equipment.EquipmentClassId
        };

        if (equipment.EquipmentClassId == null)
            return result; // طبقه‌بندی‌نشده - هیچ توصیه‌ای بدون حدس‌زدن قابل ارائه نیست

        var classId = equipment.EquipmentClassId.Value;

        var equipmentClass = await _context.EquipmentClasses
            .AsNoTracking()
            .Where(x => x.EquipmentClassId == classId)
            .Select(x => new { x.Code, x.Name })
            .FirstOrDefaultAsync();

        result.EquipmentClassCode = equipmentClass?.Code;
        result.EquipmentClassName = equipmentClass?.Name;

        result.ApplicableActivities = await _context.MaintenanceActivityEquipmentClasses
            .AsNoTracking()
            .Where(x => x.EquipmentClassId == classId && x.MaintenanceActivity!.IsActive)
            .Select(x => new MaintenanceActivitySummaryDto
            {
                MaintenanceActivityId = x.MaintenanceActivityId,
                Code = x.MaintenanceActivity!.Code,
                Name = x.MaintenanceActivity.Name,
                MaintenanceGroupName = x.MaintenanceActivity.MaintenanceGroup != null ? x.MaintenanceActivity.MaintenanceGroup.Name : null
            })
            .ToListAsync();

        var activityIds = result.ApplicableActivities.Select(x => x.MaintenanceActivityId).ToList();

        if (activityIds.Count > 0)
        {
            result.RecommendedStrategyTypes = await _context.Set<PEMS.Domain.Entities.MaintenanceActivityStrategyType>()
                .AsNoTracking()
                .Where(x => activityIds.Contains(x.MaintenanceActivityId))
                .Select(x => new LookupSummaryDto
                {
                    Id = x.MaintenanceStrategyTypeId,
                    Code = x.MaintenanceStrategyType!.Code,
                    Name = x.MaintenanceStrategyType.Name
                })
                .Distinct()
                .ToListAsync();

            result.ApplicableStandardReferences = await _context.Set<PEMS.Domain.Entities.MaintenanceActivityStandardReference>()
                .AsNoTracking()
                .Where(x => activityIds.Contains(x.MaintenanceActivityId))
                .Select(x => new LookupSummaryDto
                {
                    Id = x.StandardReferenceId,
                    Code = x.StandardReference!.Code,
                    Name = x.StandardReference.Title
                })
                .Distinct()
                .ToListAsync();
        }

        result.CandidatePMTemplates = await _context.PMTemplates
            .AsNoTracking()
            .Where(x => x.EquipmentClassId == classId && x.IsActive)
            .Select(x => new PMTemplateSummaryDto
            {
                PMTemplateId = x.PMTemplateId,
                Code = x.Code,
                Name = x.Name,
                TriggerType = x.TriggerType,
                IntervalDays = x.IntervalDays
            })
            .ToListAsync();

        result.CandidateJobPlans = await _context.JobPlans
            .AsNoTracking()
            .Where(x => x.EquipmentClassId == classId && x.IsActive)
            .Select(x => new JobPlanSummaryDto
            {
                JobPlanId = x.JobPlanId,
                JobPlanNumber = x.JobPlanNumber,
                Title = x.Title
            })
            .ToListAsync();

        result.ApplicableFailureModes = await _context.FailureModes
            .AsNoTracking()
            .Where(x => x.EquipmentClassId == classId && x.IsActive)
            .Select(x => new LookupSummaryDto
            {
                Id = x.FailureModeId,
                Code = x.Code,
                Name = x.Name
            })
            .ToListAsync();

        return result;
    }
}
