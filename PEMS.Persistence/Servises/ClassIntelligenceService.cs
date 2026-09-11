using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class ClassIntelligenceService : IClassIntelligenceService
{
    private readonly PEMSDbContext _context;
    private readonly IEquipmentClassCoverageService _coverageService;
    private readonly IReliabilityAnalyticsService _analyticsService;
    private readonly IReliabilityIntelligenceService _intelligenceService;
    private readonly IRcmFmeaReadinessService _rcmFmeaReadinessService;

    public ClassIntelligenceService(PEMSDbContext context, IEquipmentClassCoverageService coverageService,
        IReliabilityAnalyticsService analyticsService, IReliabilityIntelligenceService intelligenceService,
        IRcmFmeaReadinessService rcmFmeaReadinessService)
    {
        _context = context;
        _coverageService = coverageService;
        _analyticsService = analyticsService;
        _intelligenceService = intelligenceService;
        _rcmFmeaReadinessService = rcmFmeaReadinessService;
    }

    public async Task<ClassIntelligenceProfileDto?> GetClassIntelligenceAsync(int equipmentClassId)
    {
        var equipmentClass = await _context.EquipmentClasses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EquipmentClassId == equipmentClassId);
        if (equipmentClass == null) return null;

        var dto = new ClassIntelligenceProfileDto
        {
            EquipmentClassId = equipmentClass.EquipmentClassId,
            Code = equipmentClass.Code,
            Name = equipmentClass.Name
        };

        var coverageMatrix = await _coverageService.GetCoverageMatrixAsync();
        var coverageRow = coverageMatrix.Classes.FirstOrDefault(x => x.EquipmentClassId == equipmentClassId);
        if (coverageRow != null)
        {
            dto.EquipmentCount = coverageRow.EquipmentCount;
            dto.TotalActivityCoverage = coverageRow.TotalActivityCoverage;
            dto.TotalPMTemplateCoverage = coverageRow.TotalPMTemplateCoverage;
            dto.TotalFailureModeCoverage = coverageRow.TotalFailureModeCoverage;
            dto.CoverageGapReasons = coverageRow.GapReasons;
        }

        var reliability = await _analyticsService.GetClassReliabilityAsync(equipmentClassId);
        if (reliability != null)
        {
            dto.FailureCount = reliability.FailureCount;
            dto.AverageMTBFHours = reliability.AverageMTBFHours;
            dto.AverageMTTRHours = reliability.AverageMTTRHours;
            dto.AverageAvailabilityPercent = reliability.AverageAvailabilityPercent;
            dto.MTBFSampleSize = reliability.MTBFSampleSize;
        }

        var rcmFmea = await _rcmFmeaReadinessService.GetForEquipmentClassAsync(equipmentClassId);
        if (rcmFmea != null)
        {
            dto.FMEAReadiness = rcmFmea.FMEAReadiness;
            dto.RCMReadiness = rcmFmea.RCMReadiness;
            dto.ApprovedFMEACount = rcmFmea.ApprovedFMEACount;
            dto.ApprovedRCMCount = rcmFmea.ApprovedRCMCount;
        }

        var equipmentIdsInClass = await _context.Equipments.AsNoTracking()
            .Where(x => x.IsActive && x.EquipmentClassId == equipmentClassId)
            .Select(x => x.EquipmentId)
            .ToListAsync();

        if (equipmentIdsInClass.Count > 0)
        {
            dto.CriticalityDistribution = await _context.EquipmentCriticalities.AsNoTracking()
                .Where(x => x.IsActive && equipmentIdsInClass.Contains(x.EquipmentId))
                .GroupBy(x => x.CriticalityClass)
                .Select(g => new { Criticality = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Criticality, x => x.Count);

            var repeatFailures = await _intelligenceService.GetRepeatFailuresAsync(null, null, 90);
            dto.RepeatFailureGroupCount = repeatFailures.Count(x => equipmentIdsInClass.Contains(x.EquipmentId));

            var badActors = await _analyticsService.GetBadActorsAsync(null, null, top: 1000);
            dto.BadActorCount = badActors.Count(x => equipmentIdsInClass.Contains(x.EquipmentId));
        }

        return dto;
    }
}
