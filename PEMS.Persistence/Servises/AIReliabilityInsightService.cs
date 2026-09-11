using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PEMS.AI.Interfaces;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۷. GenerateInsightsAsync فقط از داده‌ی واقعی از قبل محاسبه‌شده
/// (BadActors/Pareto/PmCompliance از سرویس‌های موجود فاز ۷) یک Prompt می‌سازد و به Ollama
/// محلی می‌دهد - هرگز به مدل نمی‌گوید داده بسازد. اگر داده‌ی واقعی کافی نباشد، اصلاً Ollama
/// فراخوانی نمی‌شود (تا یک متن بی‌پایه تولید نشود). اگر Ollama در دسترس نباشد، خطا به‌صورت
/// شفاف گزارش می‌شود - هیچ بینش جعلی ذخیره نمی‌شود.
/// </summary>
public class AIReliabilityInsightService : IAIReliabilityInsightService
{
    private readonly PEMSDbContext _context;
    private readonly IOllamaService _ollamaService;
    private readonly IReliabilityAnalyticsService _analyticsService;
    private readonly IReliabilityIntelligenceService _intelligenceService;
    private readonly IConfiguration _configuration;

    public AIReliabilityInsightService(PEMSDbContext context, IOllamaService ollamaService,
        IReliabilityAnalyticsService analyticsService, IReliabilityIntelligenceService intelligenceService,
        IConfiguration configuration)
    {
        _context = context;
        _ollamaService = ollamaService;
        _analyticsService = analyticsService;
        _intelligenceService = intelligenceService;
        _configuration = configuration;
    }

    public async Task<GenerateInsightsResultDto> GenerateInsightsAsync(int? equipmentClassId, string? generatedBy)
    {
        var badActors = await _analyticsService.GetBadActorsAsync(null, null, top: 5);
        var pareto = await _analyticsService.GetFailureModeParetoAsync(null, null, top: 5);
        var pmCompliance = await _intelligenceService.GetPmComplianceAsync(null, null, equipmentClassId);

        if (badActors.Count == 0 && pareto.Count == 0 && pmCompliance.TotalQualifyingWorkOrders == 0)
        {
            return new GenerateInsightsResultDto
            {
                Success = false,
                Error = "داده‌ی واقعی کافی (Bad Actor/Pareto/PM Compliance) برای تولید بینش وجود ندارد - هیچ درخواستی به مدل هوش‌مصنوعی ارسال نشد.",
                InsightsGenerated = 0
            };
        }

        var sourceSummaryLines = new List<string>();
        if (badActors.Count > 0)
            sourceSummaryLines.Add($"Bad Actors (top {badActors.Count}): " + string.Join("; ", badActors.Select(b => $"{b.TagNumber}={b.FailureCount} failures/{b.TotalDowntimeHours}h downtime")));
        if (pareto.Count > 0)
            sourceSummaryLines.Add($"Failure Mode Pareto (top {pareto.Count}): " + string.Join("; ", pareto.Select(p => $"{p.Key}={p.Count}")));
        if (pmCompliance.TotalQualifyingWorkOrders > 0)
            sourceSummaryLines.Add($"PM Compliance: {pmCompliance.CompliancePercent?.ToString("N1") ?? "N/A"}% ({pmCompliance.DataStatus}, n={pmCompliance.TotalQualifyingWorkOrders})");

        var sourceSummary = string.Join(" | ", sourceSummaryLines);

        var prompt =
            "You are a reliability engineering assistant. Based ONLY on the following real, calculated plant data, " +
            "suggest up to 3 short candidate investigation areas or maintenance-action candidates for a human reliability engineer to review. " +
            "Do NOT invent numbers not present below. One suggestion per line, no preamble.\n\n" +
            sourceSummary;

        string modelOutput;
        try
        {
            modelOutput = await _ollamaService.GenerateAsync(prompt);
        }
        catch (Exception ex)
        {
            return new GenerateInsightsResultDto
            {
                Success = false,
                Error = $"فراخوانی مدل هوش‌مصنوعی محلی (Ollama) ناموفق بود: {ex.Message}",
                InsightsGenerated = 0,
                SourceDataSummary = sourceSummary
            };
        }

        var modelName = _configuration["Ollama:Model"] ?? "ollama (unspecified model)";
        var candidateLines = modelOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 0)
            .Take(5)
            .ToList();

        if (candidateLines.Count == 0)
        {
            return new GenerateInsightsResultDto
            {
                Success = false,
                Error = "مدل هوش‌مصنوعی هیچ خروجی قابل‌استفاده‌ای برنگرداند.",
                InsightsGenerated = 0,
                SourceDataSummary = sourceSummary
            };
        }

        var now = DateTime.Now;
        foreach (var line in candidateLines)
        {
            await _context.AIReliabilityInsights.AddAsync(new AIReliabilityInsight
            {
                EquipmentClassId = equipmentClassId,
                InsightType = "InvestigationSuggestion",
                SuggestionText = line,
                SourceDataSummary = sourceSummary,
                ModelName = modelName,
                Confidence = null,
                ReviewStatus = "Draft",
                GeneratedDate = now,
                IsActive = true,
                CreatedDate = now,
                CreatedBy = generatedBy
            });
        }

        await _context.SaveChangesAsync();

        return new GenerateInsightsResultDto
        {
            Success = true,
            InsightsGenerated = candidateLines.Count,
            SourceDataSummary = sourceSummary
        };
    }

    public async Task<List<AIReliabilityInsightDto>> GetInsightsAsync(string? reviewStatus)
    {
        var query = _context.AIReliabilityInsights.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(reviewStatus))
            query = query.Where(x => x.ReviewStatus == reviewStatus);

        var raw = await query
            .OrderByDescending(x => x.GeneratedDate)
            .Take(200)
            .ToListAsync();

        var equipmentIds = raw.Where(x => x.EquipmentId.HasValue).Select(x => x.EquipmentId!.Value).Distinct().ToList();
        var equipmentById = await _context.Equipments.Where(x => equipmentIds.Contains(x.EquipmentId)).ToDictionaryAsync(x => x.EquipmentId);

        var classIds = raw.Where(x => x.EquipmentClassId.HasValue).Select(x => x.EquipmentClassId!.Value).Distinct().ToList();
        var classById = await _context.EquipmentClasses.Where(x => classIds.Contains(x.EquipmentClassId)).ToDictionaryAsync(x => x.EquipmentClassId);

        var reviewerIds = raw.Where(x => x.ReviewedById.HasValue).Select(x => x.ReviewedById!.Value).Distinct().ToList();
        var reviewerById = await _context.Employees.Where(x => reviewerIds.Contains(x.EmployeeId)).ToDictionaryAsync(x => x.EmployeeId);

        return raw.Select(x => new AIReliabilityInsightDto
        {
            AIReliabilityInsightId = x.AIReliabilityInsightId,
            EquipmentId = x.EquipmentId,
            EquipmentTagNumber = x.EquipmentId.HasValue && equipmentById.TryGetValue(x.EquipmentId.Value, out var eq) ? eq.TagNumber : null,
            EquipmentClassId = x.EquipmentClassId,
            EquipmentClassName = x.EquipmentClassId.HasValue && classById.TryGetValue(x.EquipmentClassId.Value, out var cls) ? cls.Name : null,
            InsightType = x.InsightType,
            SuggestionText = x.SuggestionText,
            SourceDataSummary = x.SourceDataSummary,
            ModelName = x.ModelName,
            Confidence = x.Confidence,
            ReviewStatus = x.ReviewStatus,
            ReviewedByName = x.ReviewedById.HasValue && reviewerById.TryGetValue(x.ReviewedById.Value, out var rv) ? rv.FirstName + " " + rv.LastName : null,
            ReviewedDate = x.ReviewedDate,
            ReviewComment = x.ReviewComment,
            GeneratedDate = x.GeneratedDate
        }).ToList();
    }

    public async Task<(bool Success, string? Error)> ReviewInsightAsync(int insightId, bool approve, int? reviewedByEmployeeId, string? notes)
    {
        var insight = await _context.AIReliabilityInsights.FirstOrDefaultAsync(x => x.AIReliabilityInsightId == insightId && x.IsActive);
        if (insight == null) return (false, "بینش یافت نشد.");

        if (insight.ReviewStatus != "Draft")
            return (false, $"فقط بینش‌های Draft قابل‌بازبینی هستند (وضعیت فعلی: {insight.ReviewStatus}).");

        insight.ReviewStatus = approve ? "Approved" : "Rejected";
        insight.ReviewedById = reviewedByEmployeeId;
        insight.ReviewedDate = DateTime.Now;
        insight.ReviewComment = notes;

        // طبق بند ۱۷: عمداً هیچ نوشتن دیگری این‌جا نیست - تایید یک بینش هرگز هیچ رکورد معتبر
        // دیگری (RCMDecision/MaintenanceStrategy/EquipmentCriticality/FailureRecord/WorkOrder)
        // را تغییر نمی‌دهد. اگر مهندس بخواهد بر اساس این بینش اقدام کند، باید از طریق همان
        // گردش‌کار موجود (RCM/CriticalityAssessment/...) و با مجوز خودش اقدام کند.
        await _context.SaveChangesAsync();

        return (true, null);
    }
}
