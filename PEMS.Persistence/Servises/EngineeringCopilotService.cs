using Microsoft.EntityFrameworkCore;
using PEMS.AI.Interfaces;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class EngineeringCopilotService : IEngineeringCopilotService
{
    private readonly PEMSDbContext _context;
    private readonly IOllamaService _ollamaService;
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly IRelationshipService _relationshipService;

    public EngineeringCopilotService(
        PEMSDbContext context, IOllamaService ollamaService,
        IKnowledgeBaseService knowledgeBaseService, IRelationshipService relationshipService)
    {
        _context = context;
        _ollamaService = ollamaService;
        _knowledgeBaseService = knowledgeBaseService;
        _relationshipService = relationshipService;
    }


    // ==============================================================================
    // طبق فاز ۴ نقشه‌راه: پیشنهاد MOC بر اساس الگوی NCR های تکراری - قبلاً کاملاً غایب بود.
    // منطق کاملاً قطعی است (نه AI حدسی): شمارش واقعی NCR به تفکیک تجهیز از دیتابیس.
    // ==============================================================================
    public async Task<List<RecurringNcrPatternDto>> GetRecurringNcrPatternsAsync(
        int? projectId = null, int minOccurrences = 3, int lookbackDays = 180)
    {
        var cutoff = DateTime.Now.AddDays(-lookbackDays);

        var query = _context.NonConformanceReports
            .Include(x => x.QCDiscipline)
            .Include(x => x.Equipment)
            .Where(x => x.IsActive && x.EquipmentId.HasValue && x.RaisedDate >= cutoff);

        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);

        var ncrs = await query.ToListAsync();

        var grouped = ncrs
            .GroupBy(x => x.EquipmentId!.Value)
            .Where(g => g.Count() >= minOccurrences)
            .ToList();

        if (grouped.Count == 0) return new List<RecurringNcrPatternDto>();

        var equipmentIds = grouped.Select(g => g.Key).ToList();

        // طبق همون اصل جلوگیری از N+1: یک Query برای همه‌ی MOC های باز مرتبط با این تجهیزات
        var openMocEquipmentIds = new HashSet<int>(await _context.MOCs
            .Where(x => x.IsActive && x.RelatedEquipmentId.HasValue
                && equipmentIds.Contains(x.RelatedEquipmentId.Value)
                && x.Status != "Closed" && x.Status != "Rejected")
            .Select(x => x.RelatedEquipmentId!.Value)
            .Distinct()
            .ToListAsync());

        var results = new List<RecurringNcrPatternDto>();

        foreach (var g in grouped.OrderByDescending(x => x.Count()))
        {
            var equipment = g.First().Equipment;
            if (equipment == null) continue;

            var dominantDiscipline = g
                .Where(x => x.QCDiscipline != null)
                .GroupBy(x => x.QCDiscipline!.Name)
                .OrderByDescending(x => x.Count())
                .FirstOrDefault()?.Key;

            var hasOpenMoc = openMocEquipmentIds.Contains(g.Key);

            results.Add(new RecurringNcrPatternDto
            {
                EquipmentId = g.Key,
                EquipmentTagNumber = equipment.TagNumber,
                EquipmentName = equipment.Name,
                NcrCount = g.Count(),
                NcrNumbers = g.Select(x => x.NCRNumber).ToList(),
                DominantQCDiscipline = dominantDiscipline,
                HasOpenMOC = hasOpenMoc,
                Suggestion = hasOpenMoc
                    ? "یک MOC باز از قبل برای این تجهیز وجود دارد - این الگو رو در نظر بگیر."
                    : $"{g.Count()} بار NCR در {lookbackDays} روز اخیر - پیشنهاد می‌شود یک MOC برای بررسی ریشه‌ای این تجهیز ثبت شود."
            });
        }

        return results;
    }


    // ==============================================================================
    // طبق فاز ۴ نقشه‌راه: تحلیل علت تاخیر با داده‌ی واقعی سیستم (نه فرضی). ارقام از دیتابیس
    // واقعی محاسبه می‌شوند؛ Ollama فقط یک خلاصه‌ی روایی از همین ارقام واقعی می‌نویسد - هرگز
    // به Ollama اجازه داده نمی‌شود عدد از خودش بسازد.
    // ==============================================================================
    public async Task<DelayRootCauseAnalysisDto> GetDelayRootCauseAnalysisAsync(int? projectId = null)
    {
        var docsQuery = _context.Documents
            .Include(x => x.Discipline)
            .Where(x => x.IsActive && x.PlannedDate.HasValue);

        if (projectId.HasValue) docsQuery = docsQuery.Where(x => x.ProjectId == projectId.Value);

        // DelayDays یک Property محاسبه‌شده در حافظه است (نه ستون دیتابیس) - باید بعد از
        // واکشی محاسبه بشه، نه داخل Query
        var allDocs = await docsQuery.ToListAsync();
        var delayedDocs = allDocs.Where(x => x.DelayDays.HasValue && x.DelayDays.Value > 0).ToList();

        var result = new DelayRootCauseAnalysisDto
        {
            TotalDelayedDocuments = delayedDocs.Count,
            AverageDelayDays = delayedDocs.Count > 0 ? Math.Round(delayedDocs.Average(x => x.DelayDays!.Value), 1) : 0
        };

        if (delayedDocs.Count == 0) return result;

        result.ByDiscipline = delayedDocs
            .GroupBy(x => x.Discipline?.Name ?? "نامشخص")
            .Select(g => (g.Key, g.Count(), Math.Round(g.Average(x => x.DelayDays!.Value), 1)))
            .OrderByDescending(x => x.Item2)
            .Take(10)
            .ToList();

        result.ByStage = delayedDocs
            .GroupBy(x => x.Status)
            .Select(g => (g.Key, g.Count(), Math.Round(g.Average(x => x.DelayDays!.Value), 1)))
            .OrderByDescending(x => x.Item2)
            .Take(10)
            .ToList();

        // Vendor فقط برای اسناد VDR معنی داره - جدا واکشی می‌شه چون روی Document پایه نیست
        var delayedVdrIds = delayedDocs.Where(x => x.DocumentKind == "Vendor").Select(x => x.DocumentId).ToList();
        if (delayedVdrIds.Count > 0)
        {
            var vdrWithVendor = await _context.VDRItems
                .Include(x => x.Vendor)
                .Where(x => delayedVdrIds.Contains(x.DocumentId))
                .Select(x => new { x.DocumentId, VendorName = x.Vendor != null ? x.Vendor.Name : "نامشخص" })
                .ToListAsync();

            var delayById = delayedDocs.ToDictionary(x => x.DocumentId, x => x.DelayDays!.Value);

            result.ByVendor = vdrWithVendor
                .GroupBy(x => x.VendorName)
                .Select(g => (g.Key, g.Count(), Math.Round(g.Average(x => (double)delayById.GetValueOrDefault(x.DocumentId, 0)), 1)))
                .OrderByDescending(x => x.Item2)
                .Take(10)
                .ToList();
        }

        // خلاصه‌ی روایی - فقط از روی همین ارقام محاسبه‌شده، نه از خیال Ollama
        try
        {
            var topDiscipline = result.ByDiscipline.FirstOrDefault();
            var topStage = result.ByStage.FirstOrDefault();
            var topVendor = result.ByVendor.FirstOrDefault();

            var prompt = $@"طبق داده‌ی واقعی سیستم PEMS، تحلیل کوتاه (حداکثر ۴ جمله، فارسی) از الگوی تاخیر مدارک بنویس. فقط از همین ارقام استفاده کن، هیچ عدد جدیدی نساز:
- تعداد کل مدارک با تاخیر: {result.TotalDelayedDocuments}
- میانگین تاخیر: {result.AverageDelayDays} روز
- بیشترین تاخیر به تفکیک دیسیپلین: {(topDiscipline.GroupName != null ? $"{topDiscipline.GroupName} ({topDiscipline.Count} مورد، میانگین {topDiscipline.AvgDelayDays} روز)" : "-")}
- بیشترین تاخیر به تفکیک وضعیت: {(topStage.GroupName != null ? $"{topStage.GroupName} ({topStage.Count} مورد)" : "-")}
- بیشترین تاخیر به تفکیک وندور: {(topVendor.GroupName != null ? $"{topVendor.GroupName} ({topVendor.Count} مورد)" : "-")}";

            result.NarrativeSummary = await _ollamaService.GenerateAsync(prompt);
        }
        catch
        {
            // Ollama در دسترس نیست - فقط جدول‌های عددی (که از دیتابیس واقعی محاسبه شدن) نمایش داده می‌شن
            result.NarrativeSummary = null;
        }

        return result;
    }


    // ==============================================================================
    // طبق فاز ۴ نقشه‌راه: کشف رابطه‌ی معنایی این مدرک با بقیه (تکمیل تشخیص متن‌محور موجود
    // در IntakeService). از همون IKnowledgeBaseService موجود استفاده می‌کند - نه پیاده‌سازی
    // جدید Embedding.
    // ==============================================================================
    public async Task<int> AnalyzeDocumentRelationshipsAsync(int documentId)
    {
        var document = await _context.Documents.FirstOrDefaultAsync(x => x.DocumentId == documentId);
        if (document == null) return 0;

        var content = string.Join(" ", new[] { document.DocumentNo, document.Title }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrWhiteSpace(content)) return 0;

        var added = 0;

        try
        {
            var semanticResults = await _knowledgeBaseService.SearchAsync(content, topK: 8);

            // طبق محدودیت شناخته‌شده: MergeAIProposedRelationshipsAsync فعلاً فقط
            // TargetType == "Document" یا "Equipment" را نمایش می‌دهد - برای جلوگیری از
            // ساخت رابطه‌ای که هیچ‌جا دیده نمی‌شود، فعلاً فقط به مدارک مهندسی محدود می‌کنیم
            foreach (var r in semanticResults.Where(r =>
                r.SourceType == "EngineeringDocument" &&
                r.SimilarityScore >= 0.6 &&
                r.SourceId != documentId))
            {
                await _relationshipService.AddAIProposedRelationshipAsync(
                    "Document", documentId, "Document", r.SourceId, "SemanticallyRelated", r.SimilarityScore);

                added++;
            }
        }
        catch
        {
            // Ollama/Embedding در دسترس نیست - بدون خطا، فقط صفر رابطه‌ی جدید پیدا شد
        }

        return added;
    }
}
