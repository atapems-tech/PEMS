using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// ==============================================================================
/// طبق «PEMS Help System — Phase 3». نکته‌ی امنیتی مهم (بخش ۶/۱۳ سند): هیچ HTML خام هرگز در
/// یک HelpVisual ذخیره یا رندر نمی‌شود - برچسب‌های Node/Edge همیشه متن ساده‌اند (نه از طریق
/// IRichTextSanitizerService پاک‌سازی می‌شوند، چون اصلاً مجاز به داشتن تگ نیستند؛ Razor خودش
/// در زمان رندر Encode می‌کند). فرم ادمین یک متن ساختاریافته‌ی خط‌به‌خط می‌گیرد (نه JSON خام
/// از کلاینت) تا امکان تزریق ساختار دلخواه وجود نداشته باشد.
/// ==============================================================================
/// </summary>
public class HelpVisualService : IHelpVisualService
{
    private readonly PEMSDbContext _context;
    private readonly IHelpWorkflowDiagramService _workflowDiagramService;

    private static readonly string[] ValidVisualTypes =
    {
        "WorkflowDiagram", "ProcessFlowchart", "LifecycleDiagram", "DecisionTree", "StepGuide", "EntityRelationship"
    };

    public HelpVisualService(PEMSDbContext context, IHelpWorkflowDiagramService workflowDiagramService)
    {
        _context = context;
        _workflowDiagramService = workflowDiagramService;
    }

    public async Task<IReadOnlyList<HelpVisualRenderDto>> GetVisualsForArticleAsync(
        int helpArticleId, string languageCode, bool includeUnpublished = false)
    {
        var lang = languageCode == "fa" ? "fa" : "en";

        var query = _context.HelpVisuals.Where(v => v.HelpArticleId == helpArticleId);
        if (!includeUnpublished) query = query.Where(v => v.IsPublished);

        var visuals = await query.OrderBy(v => v.DisplayOrder).ToListAsync();

        var results = new List<HelpVisualRenderDto>();

        foreach (var v in visuals)
        {
            HelpVisualRenderDto? dto;

            if (v.VisualType == "WorkflowDiagram")
            {
                if (string.IsNullOrWhiteSpace(v.SourceWorkflowModule) || string.IsNullOrWhiteSpace(v.SourceWorkflowCategory))
                    continue; // پیکربندی ناقص - به‌جای رندر خالی/غلط، این آیتم را رد کن

                dto = await _workflowDiagramService.BuildAsync(v.SourceWorkflowModule, v.SourceWorkflowCategory, lang);
                if (dto == null) continue; // گردش‌کار منبع دیگر وجود ندارد - هرگز چیزی جعل نکن
            }
            else
            {
                dto = BuildFromManualDefinition(v, lang);
                if (dto == null) continue;
            }

            dto.HelpVisualId = v.HelpVisualId;
            dto.VisualType = v.VisualType;
            dto.Caption = lang == "fa" ? v.CaptionFa : v.CaptionEn;
            dto.Legend = lang == "fa" ? v.LegendFa : v.LegendEn;
            dto.AccessibilityDescription = lang == "fa" ? v.AccessibilityDescriptionFa : v.AccessibilityDescriptionEn;
            dto.RelatedHelpKey = v.RelatedHelpKey;

            results.Add(dto);
        }

        return results;
    }

    private static HelpVisualRenderDto? BuildFromManualDefinition(HelpVisual v, string lang)
    {
        if (string.IsNullOrWhiteSpace(v.DefinitionJson)) return null;

        HelpVisualDefinition? def;
        try
        {
            def = JsonSerializer.Deserialize<HelpVisualDefinition>(v.DefinitionJson);
        }
        catch (JsonException)
        {
            return null; // طبق بخش ۱۵ سند: تعریف نامعتبر باید Fallback امن داشته باشد، نه Exception به کاربر
        }

        if (def == null || def.Nodes.Count == 0) return null;

        return new HelpVisualRenderDto
        {
            LanguageCode = lang,
            Nodes = def.Nodes.Select(n => new HelpVisualRenderNode
            {
                Id = n.Id,
                Label = lang == "fa" ? n.LabelFa : n.LabelEn,
                IconCssClass = n.IconCssClass
            }).ToList(),
            Edges = def.Edges.Select(e => new HelpVisualRenderEdge
            {
                FromNodeId = e.FromNodeId,
                ToNodeId = e.ToNodeId,
                Label = lang == "fa" ? e.LabelFa : e.LabelEn,
                Kind = e.Kind
            }).ToList()
        };
    }

    public async Task<IReadOnlyList<HelpVisualAdminEditDto>> GetAllForArticleAdminAsync(int helpArticleId)
    {
        var visuals = await _context.HelpVisuals
            .Where(v => v.HelpArticleId == helpArticleId)
            .OrderBy(v => v.DisplayOrder)
            .ToListAsync();

        return visuals.Select(ToAdminDto).ToList();
    }

    public async Task<HelpVisualAdminEditDto?> GetForEditAsync(int helpVisualId)
    {
        var v = await _context.HelpVisuals.FirstOrDefaultAsync(x => x.HelpVisualId == helpVisualId);
        return v == null ? null : ToAdminDto(v);
    }

    public async Task<int> CreateAsync(HelpVisualAdminEditDto dto)
    {
        Validate(dto);

        var visual = new HelpVisual
        {
            HelpArticleId = dto.HelpArticleId,
            VisualType = dto.VisualType,
            DisplayOrder = dto.DisplayOrder,
            IsPublished = dto.IsPublished,
            SourceWorkflowModule = dto.SourceWorkflowModule,
            SourceWorkflowCategory = dto.SourceWorkflowCategory,
            DefinitionJson = dto.VisualType == "WorkflowDiagram" ? null : ParseManualDefinition(dto.ManualDefinitionText),
            CaptionEn = dto.CaptionEn.Trim(),
            CaptionFa = dto.CaptionFa.Trim(),
            LegendEn = dto.LegendEn,
            LegendFa = dto.LegendFa,
            AccessibilityDescriptionEn = dto.AccessibilityDescriptionEn.Trim(),
            AccessibilityDescriptionFa = dto.AccessibilityDescriptionFa.Trim(),
            RelatedHelpKey = dto.RelatedHelpKey,
            CreatedDate = DateTime.Now
        };

        _context.HelpVisuals.Add(visual);
        await _context.SaveChangesAsync();
        return visual.HelpVisualId;
    }

    public async Task UpdateAsync(int helpVisualId, HelpVisualAdminEditDto dto)
    {
        Validate(dto);

        var visual = await _context.HelpVisuals.FirstOrDefaultAsync(x => x.HelpVisualId == helpVisualId);
        if (visual == null) throw new InvalidOperationException("محتوای بصری یافت نشد.");

        visual.VisualType = dto.VisualType;
        visual.DisplayOrder = dto.DisplayOrder;
        visual.IsPublished = dto.IsPublished;
        visual.SourceWorkflowModule = dto.SourceWorkflowModule;
        visual.SourceWorkflowCategory = dto.SourceWorkflowCategory;
        visual.DefinitionJson = dto.VisualType == "WorkflowDiagram" ? null : ParseManualDefinition(dto.ManualDefinitionText);
        visual.CaptionEn = dto.CaptionEn.Trim();
        visual.CaptionFa = dto.CaptionFa.Trim();
        visual.LegendEn = dto.LegendEn;
        visual.LegendFa = dto.LegendFa;
        visual.AccessibilityDescriptionEn = dto.AccessibilityDescriptionEn.Trim();
        visual.AccessibilityDescriptionFa = dto.AccessibilityDescriptionFa.Trim();
        visual.RelatedHelpKey = dto.RelatedHelpKey;
        visual.ModifiedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    public async Task SetPublishedAsync(int helpVisualId, bool isPublished)
    {
        var visual = await _context.HelpVisuals.FirstOrDefaultAsync(x => x.HelpVisualId == helpVisualId);
        if (visual == null) throw new InvalidOperationException("محتوای بصری یافت نشد.");

        visual.IsPublished = isPublished;
        visual.ModifiedDate = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int helpVisualId)
    {
        var visual = await _context.HelpVisuals.FirstOrDefaultAsync(x => x.HelpVisualId == helpVisualId);
        if (visual == null) return;

        _context.HelpVisuals.Remove(visual);
        await _context.SaveChangesAsync();
    }

    // ============ Helpers ============

    private static void Validate(HelpVisualAdminEditDto dto)
    {
        if (!ValidVisualTypes.Contains(dto.VisualType))
            throw new InvalidOperationException($"نوع محتوای بصری نامعتبر است: {dto.VisualType}");

        if (string.IsNullOrWhiteSpace(dto.CaptionEn) || string.IsNullOrWhiteSpace(dto.CaptionFa))
            throw new InvalidOperationException("عنوان کوتاه (Caption) به هر دو زبان الزامی است.");

        if (string.IsNullOrWhiteSpace(dto.AccessibilityDescriptionEn) || string.IsNullOrWhiteSpace(dto.AccessibilityDescriptionFa))
            throw new InvalidOperationException("توضیح دسترس‌پذیری (Accessibility Description) به هر دو زبان الزامی است - این تنها راه فهمیدن نمودار برای صفحه‌خوان‌هاست.");

        if (dto.VisualType == "WorkflowDiagram")
        {
            if (string.IsNullOrWhiteSpace(dto.SourceWorkflowModule) || string.IsNullOrWhiteSpace(dto.SourceWorkflowCategory))
                throw new InvalidOperationException("برای Workflow Diagram، انتخاب Module/Category واقعی الزامی است.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(dto.ManualDefinitionText))
                throw new InvalidOperationException("برای این نوع محتوای بصری، تعریف Node/Edge الزامی است.");
        }
    }

    /// <summary>
    /// طبق بخش ۱۳ سند: به‌جای گرفتن JSON خام از کلاینت، یک فرمت متنیِ خط‌به‌خطِ محدود پارس
    /// می‌شود - سطح حمله بسیار کوچک‌تر از پذیرفتن JSON دلخواه است، و برای ادمین هم ساده‌تر.
    ///   NODE|id|labelEn|labelFa[|iconCssClass]
    ///   EDGE|fromId|toIdOrEmpty|labelEn|labelFa|kind
    /// </summary>
    private static string? ParseManualDefinition(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var def = new HelpVisualDefinition();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            var parts = line.Split('|');

            if (parts[0].Equals("NODE", StringComparison.OrdinalIgnoreCase) && parts.Length >= 4)
            {
                def.Nodes.Add(new HelpVisualNodeDto
                {
                    Id = parts[1].Trim(),
                    LabelEn = parts[2].Trim(),
                    LabelFa = parts[3].Trim(),
                    IconCssClass = parts.Length >= 5 && !string.IsNullOrWhiteSpace(parts[4]) ? parts[4].Trim() : null
                });
            }
            else if (parts[0].Equals("EDGE", StringComparison.OrdinalIgnoreCase) && parts.Length >= 6)
            {
                def.Edges.Add(new HelpVisualEdgeDto
                {
                    FromNodeId = parts[1].Trim(),
                    ToNodeId = string.IsNullOrWhiteSpace(parts[2]) ? null : parts[2].Trim(),
                    LabelEn = parts[3].Trim(),
                    LabelFa = parts[4].Trim(),
                    Kind = parts[5].Trim()
                });
            }
        }

        if (def.Nodes.Count == 0)
            throw new InvalidOperationException("حداقل یک خط NODE معتبر لازم است (فرمت: NODE|id|labelEn|labelFa).");

        var nodeIds = def.Nodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalidEdge = def.Edges.FirstOrDefault(e =>
            !nodeIds.Contains(e.FromNodeId) || (e.ToNodeId != null && !nodeIds.Contains(e.ToNodeId)));

        if (invalidEdge != null)
            throw new InvalidOperationException($"یک خط EDGE به گره‌ی نامعتبری ارجاع می‌دهد: {invalidEdge.FromNodeId} -> {invalidEdge.ToNodeId}");

        return JsonSerializer.Serialize(def);
    }

    private static HelpVisualAdminEditDto ToAdminDto(HelpVisual v) => new()
    {
        HelpVisualId = v.HelpVisualId,
        HelpArticleId = v.HelpArticleId,
        VisualType = v.VisualType,
        DisplayOrder = v.DisplayOrder,
        IsPublished = v.IsPublished,
        SourceWorkflowModule = v.SourceWorkflowModule,
        SourceWorkflowCategory = v.SourceWorkflowCategory,
        ManualDefinitionText = ToManualDefinitionText(v.DefinitionJson),
        CaptionEn = v.CaptionEn,
        CaptionFa = v.CaptionFa,
        LegendEn = v.LegendEn,
        LegendFa = v.LegendFa,
        AccessibilityDescriptionEn = v.AccessibilityDescriptionEn,
        AccessibilityDescriptionFa = v.AccessibilityDescriptionFa,
        RelatedHelpKey = v.RelatedHelpKey
    };

    private static string? ToManualDefinitionText(string? definitionJson)
    {
        if (string.IsNullOrWhiteSpace(definitionJson)) return null;

        HelpVisualDefinition? def;
        try { def = JsonSerializer.Deserialize<HelpVisualDefinition>(definitionJson); }
        catch (JsonException) { return null; }
        if (def == null) return null;

        var lines = new List<string>();
        lines.AddRange(def.Nodes.Select(n => $"NODE|{n.Id}|{n.LabelEn}|{n.LabelFa}|{n.IconCssClass}"));
        lines.AddRange(def.Edges.Select(e => $"EDGE|{e.FromNodeId}|{e.ToNodeId}|{e.LabelEn}|{e.LabelFa}|{e.Kind}"));
        return string.Join("\n", lines);
    }
}
