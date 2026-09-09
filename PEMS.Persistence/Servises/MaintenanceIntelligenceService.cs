using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 3 — Hierarchy-Aware Maintenance Intelligence» بند ۱۷-۲۳: تطبیق‌پذیری فقط
/// محدود به خودِ EquipmentClass تجهیز نیست - اگر یک Activity/PMTemplate/FailureMode روی کلاس
/// والد (یا جد) تعریف شده باشد و روی کلاس خودِ تجهیز چیزی مشخص نشده باشد، آن آیتم به ارث می‌رسد.
/// اولویت با نزدیک‌ترین سطح است (بند ۱۸)؛ هر آیتم دقیقاً یک‌بار برمی‌گردد. همه‌ی کوئری‌ها
/// AsNoTracking و روی یک IN() محدود به زنجیره‌ی سلسله‌مراتب (حداکثر ۱۰ سطح، محافظ در برابر حلقه)
/// اجرا می‌شوند - نه یک Full Table Scan.
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
            .Select(x => new { x.EquipmentId, x.TagNumber, x.EquipmentType, x.EquipmentClassId, x.ClassificationConfidence, x.ClassificationSource })
            .FirstOrDefaultAsync();

        if (equipment == null) return null;

        var result = new EquipmentMaintenanceIntelligenceDto
        {
            EquipmentId = equipment.EquipmentId,
            TagNumber = equipment.TagNumber,
            EquipmentType = equipment.EquipmentType,
            EquipmentClassId = equipment.EquipmentClassId,
            ClassificationConfidence = equipment.ClassificationConfidence,
            ClassificationSource = equipment.ClassificationSource
        };

        if (equipment.EquipmentClassId == null)
            return result; // طبقه‌بندی‌نشده - هیچ توصیه‌ای بدون حدس‌زدن قابل ارائه نیست

        // طبق بند ۱۸: زنجیره‌ی سلسله‌مراتب از خودِ کلاس تا ریشه - چون کل جدول EquipmentClass
        // کوچک است (~۷۰ ردیف)، یک‌بار کامل واکشی و در حافظه پیموده می‌شود؛ ارجح بر N کوئری جدا.
        var allClasses = await _context.EquipmentClasses
            .AsNoTracking()
            .Select(x => new { x.EquipmentClassId, x.Code, x.Name, x.ParentEquipmentClassId })
            .ToListAsync();

        var parentById = allClasses.ToDictionary(x => x.EquipmentClassId, x => x.ParentEquipmentClassId);
        var nameById = allClasses.ToDictionary(x => x.EquipmentClassId, x => x.Name);
        var codeById = allClasses.ToDictionary(x => x.EquipmentClassId, x => x.Code);

        var chain = new List<int>();
        int? current = equipment.EquipmentClassId;
        var guard = 0;
        while (current.HasValue && guard++ < 10 && !chain.Contains(current.Value))
        {
            chain.Add(current.Value);
            current = parentById.TryGetValue(current.Value, out var p) ? p : null;
        }

        result.EquipmentClassCode = codeById.GetValueOrDefault(chain[0]);
        result.EquipmentClassName = nameById.GetValueOrDefault(chain[0]);
        result.ResolvedClassHierarchy = chain.Select(id => nameById.GetValueOrDefault(id, "?")).ToList();

        var chainIndex = chain.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);
        string SourceLabel(int classId) => chainIndex[classId] == 0
            ? $"Exact match on {nameById.GetValueOrDefault(classId)}"
            : $"Inherited from {nameById.GetValueOrDefault(classId)}";

        // ==================== Activities (+ derived Strategy Types / Standard References) ====================
        var rawActivities = await _context.MaintenanceActivityEquipmentClasses
            .AsNoTracking()
            .Where(x => chain.Contains(x.EquipmentClassId) && x.MaintenanceActivity!.IsActive)
            .Select(x => new
            {
                x.EquipmentClassId,
                x.MaintenanceActivityId,
                Code = x.MaintenanceActivity!.Code,
                Name = x.MaintenanceActivity.Name,
                GroupName = x.MaintenanceActivity.MaintenanceGroup != null ? x.MaintenanceActivity.MaintenanceGroup.Name : null
            })
            .ToListAsync();

        result.ApplicableActivities = rawActivities
            .GroupBy(x => x.MaintenanceActivityId)
            .Select(g => g.OrderBy(x => chainIndex[x.EquipmentClassId]).First())
            .Select(x => new MaintenanceActivitySummaryDto
            {
                MaintenanceActivityId = x.MaintenanceActivityId,
                Code = x.Code,
                Name = x.Name,
                MaintenanceGroupName = x.GroupName,
                Source = SourceLabel(x.EquipmentClassId)
            })
            .OrderBy(x => x.Name)
            .ToList();

        var activityIds = result.ApplicableActivities.Select(x => x.MaintenanceActivityId).ToList();

        if (activityIds.Count > 0)
        {
            result.RecommendedStrategyTypes = (await _context.Set<MaintenanceActivityStrategyType>()
                    .AsNoTracking()
                    .Where(x => activityIds.Contains(x.MaintenanceActivityId))
                    .Select(x => new { x.MaintenanceStrategyTypeId, Code = x.MaintenanceStrategyType!.Code, Name = x.MaintenanceStrategyType.Name })
                    .ToListAsync())
                .GroupBy(x => x.MaintenanceStrategyTypeId)
                .Select(g => new LookupSummaryDto { Id = g.Key, Code = g.First().Code, Name = g.First().Name, Source = "Derived from applicable Activities" })
                .OrderBy(x => x.Name)
                .ToList();

            result.ApplicableStandardReferences = (await _context.Set<MaintenanceActivityStandardReference>()
                    .AsNoTracking()
                    .Where(x => activityIds.Contains(x.MaintenanceActivityId))
                    .Select(x => new { x.StandardReferenceId, Code = x.StandardReference!.Code, Name = x.StandardReference.Title })
                    .ToListAsync())
                .GroupBy(x => x.StandardReferenceId)
                .Select(g => new LookupSummaryDto { Id = g.Key, Code = g.First().Code, Name = g.First().Name, Source = "Derived from applicable Activities" })
                .OrderBy(x => x.Name)
                .ToList();
        }

        // ==================== PM Templates ====================
        var rawTemplates = await _context.PMTemplates
            .AsNoTracking()
            .Where(x => x.EquipmentClassId != null && chain.Contains(x.EquipmentClassId!.Value) && x.IsActive)
            .Select(x => new { x.EquipmentClassId, x.PMTemplateId, x.Code, x.Name, x.TriggerType, x.IntervalDays })
            .ToListAsync();

        result.CandidatePMTemplates = rawTemplates
            .GroupBy(x => x.PMTemplateId)
            .Select(g => g.OrderBy(x => chainIndex[x.EquipmentClassId!.Value]).First())
            .Select(x => new PMTemplateSummaryDto
            {
                PMTemplateId = x.PMTemplateId,
                Code = x.Code,
                Name = x.Name,
                TriggerType = x.TriggerType,
                IntervalDays = x.IntervalDays,
                Source = SourceLabel(x.EquipmentClassId!.Value)
            })
            .OrderBy(x => x.Name)
            .ToList();

        // ==================== Job Plans ====================
        var rawJobPlans = await _context.JobPlans
            .AsNoTracking()
            .Where(x => x.EquipmentClassId != null && chain.Contains(x.EquipmentClassId!.Value) && x.IsActive)
            .Select(x => new { x.EquipmentClassId, x.JobPlanId, x.JobPlanNumber, x.Title })
            .ToListAsync();

        result.CandidateJobPlans = rawJobPlans
            .GroupBy(x => x.JobPlanId)
            .Select(g => g.OrderBy(x => chainIndex[x.EquipmentClassId!.Value]).First())
            .Select(x => new JobPlanSummaryDto
            {
                JobPlanId = x.JobPlanId,
                JobPlanNumber = x.JobPlanNumber,
                Title = x.Title,
                Source = SourceLabel(x.EquipmentClassId!.Value)
            })
            .OrderBy(x => x.JobPlanNumber)
            .ToList();

        // ==================== Failure Modes ====================
        var rawFailureModes = await _context.FailureModes
            .AsNoTracking()
            .Where(x => x.EquipmentClassId != null && chain.Contains(x.EquipmentClassId!.Value) && x.IsActive)
            .Select(x => new { x.EquipmentClassId, x.FailureModeId, x.Code, x.Name })
            .ToListAsync();

        result.ApplicableFailureModes = rawFailureModes
            .GroupBy(x => x.FailureModeId)
            .Select(g => g.OrderBy(x => chainIndex[x.EquipmentClassId!.Value]).First())
            .Select(x => new LookupSummaryDto
            {
                Id = x.FailureModeId,
                Code = x.Code,
                Name = x.Name,
                Source = SourceLabel(x.EquipmentClassId!.Value)
            })
            .OrderBy(x => x.Name)
            .ToList();

        return result;
    }
}
