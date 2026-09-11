using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class PredictiveMaintenanceService : IPredictiveMaintenanceService
{
    private readonly PEMSDbContext _context;

    public PredictiveMaintenanceService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<PredictiveMaintenanceReadinessDto> GetReadinessAsync()
    {
        var modelCount = await _context.PredictiveMaintenanceModels.CountAsync(x => x.IsActive);
        var validatedCount = await _context.PredictiveMaintenanceModels.CountAsync(x => x.IsActive && x.ValidationStatus == "VALIDATED");
        var resultCount = await _context.PredictiveMaintenanceResults.CountAsync(x => x.IsActive);

        return new PredictiveMaintenanceReadinessDto
        {
            EngineStatus = "READY",
            ProductionModelStatus = validatedCount > 0 ? "PARTIAL" : "NOT_READY",
            ModelCount = modelCount,
            ValidatedModelCount = validatedCount,
            ResultCount = resultCount,
            Reason = validatedCount > 0
                ? $"{validatedCount} مدل با شواهد اعتبارسنجی ثبت شده، اما هنوز برای استفاده‌ی تولیدی کامل تایید نشده."
                : "هیچ مدل پیش‌بینانه‌ای هنوز با داده‌ی واقعی خرابی/نگهداری اعتبارسنجی نشده - معماری/زیرساخت آماده است، مدل تولیدی آماده نیست."
        };
    }

    public async Task<List<PredictiveMaintenanceModelDto>> GetModelsAsync()
    {
        var models = await _context.PredictiveMaintenanceModels
            .Where(x => x.IsActive)
            .ToListAsync();

        var modelIds = models.Select(x => x.PredictiveMaintenanceModelId).ToList();
        var resultCounts = await _context.PredictiveMaintenanceResults
            .Where(x => modelIds.Contains(x.PredictiveMaintenanceModelId) && x.IsActive)
            .GroupBy(x => x.PredictiveMaintenanceModelId)
            .Select(g => new { ModelId = g.Key, Count = g.Count() })
            .ToListAsync();

        return models.Select(m => new PredictiveMaintenanceModelDto
        {
            PredictiveMaintenanceModelId = m.PredictiveMaintenanceModelId,
            TargetName = m.TargetName,
            Algorithm = m.Algorithm,
            Description = m.Description,
            TrainingDataStatus = m.TrainingDataStatus,
            ValidationStatus = m.ValidationStatus,
            ResultCount = resultCounts.FirstOrDefault(x => x.ModelId == m.PredictiveMaintenanceModelId)?.Count ?? 0
        }).ToList();
    }
}
