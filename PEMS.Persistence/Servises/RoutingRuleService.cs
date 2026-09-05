using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class RoutingRuleService : IRoutingRuleService
{
    private readonly PEMSDbContext _context;
    private readonly ICacheService _cache;

    public RoutingRuleService(PEMSDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    // طبق بخش ۴ سند AI Redesign
    private static readonly (string Category, string Module, bool AutoRoute)[] Defaults =
    {
        ("Engineering Drawing", "MDR", true),
        ("Calculation", "MDR", true),
        ("Specification", "MDR", true),
        ("Vendor Drawing", "VDR", true),
        ("Vendor Datasheet", "VDR", true),
        ("Vendor Manual", "VDR", true),
        ("Material Requisition", "MR", false),
        ("Material Requisition Query", "MRQ", false),
        ("Technical Bid", "MRQ", false),
        ("Purchase Order", "MR", false),
        ("Contract", "Contract", false),
        ("Inspection Report", "CMMS", false),
        ("Commissioning Report", "CMMS", false),
        ("Construction Package", "CMMS", false),
        ("Correspondence", "Unrouted", false),
        ("Meeting Minutes", "Unrouted", false),
        ("Other", "Unrouted", false),
    };

    public async Task<List<RoutingRuleDto>> GetAllAsync()
    {
        return await _context.RoutingRules
            .OrderBy(x => x.DocumentCategory)
            .Select(x => new RoutingRuleDto
            {
                RoutingRuleId = x.RoutingRuleId,
                DocumentCategory = x.DocumentCategory,
                TargetModule = x.TargetModule,
                AllowAutoRouting = x.AllowAutoRouting,
                Description = x.Description,
                IsActive = x.IsActive
            })
            .ToListAsync();
    }

    public async Task SetRuleAsync(string category, string targetModule, bool allowAutoRouting, string? description)
    {
        var rule = await _context.RoutingRules.FirstOrDefaultAsync(x => x.DocumentCategory == category);

        if (rule == null)
        {
            rule = new RoutingRule { DocumentCategory = category, CreatedDate = DateTime.Now };
            await _context.RoutingRules.AddAsync(rule);
        }

        rule.TargetModule = targetModule;
        rule.AllowAutoRouting = allowAutoRouting;
        rule.Description = description;
        rule.IsActive = true;

        await _context.SaveChangesAsync();

        // تغییر از UI باید فوراً اثر بگذارد، نه بعد از انقضای Cache
        await _cache.RemoveAsync($"routing-rule:module:{category}");
        await _cache.RemoveAsync($"routing-rule:auto:{category}");
    }

    public async Task<string?> GetTargetModuleAsync(string category)
    {
        return await _cache.GetOrCreateAsync($"routing-rule:module:{category}", async () =>
        {
            var rule = await _context.RoutingRules
                .FirstOrDefaultAsync(x => x.DocumentCategory == category && x.IsActive);

            return rule?.TargetModule ?? "Unrouted";
        });
    }

    public async Task<bool> AllowsAutoRoutingAsync(string category)
    {
        return await _cache.GetOrCreateAsync($"routing-rule:auto:{category}", async () =>
        {
            var rule = await _context.RoutingRules
                .FirstOrDefaultAsync(x => x.DocumentCategory == category && x.IsActive);

            return rule?.AllowAutoRouting ?? false;
        });
    }

    public async Task<AIConfidenceSettingsDto> GetConfidenceSettingsAsync()
    {
        var settings = await _context.AIConfidenceSettings.FirstOrDefaultAsync();

        if (settings == null)
        {
            settings = new AIConfidenceSettings();
            await _context.AIConfidenceSettings.AddAsync(settings);
            await _context.SaveChangesAsync();
        }

        return new AIConfidenceSettingsDto
        {
            HighThreshold = settings.HighThreshold,
            MediumThreshold = settings.MediumThreshold
        };
    }

    public async Task UpdateConfidenceSettingsAsync(double highThreshold, double mediumThreshold)
    {
        var settings = await _context.AIConfidenceSettings.FirstOrDefaultAsync();

        if (settings == null)
        {
            settings = new AIConfidenceSettings();
            await _context.AIConfidenceSettings.AddAsync(settings);
        }

        settings.HighThreshold = highThreshold;
        settings.MediumThreshold = mediumThreshold;
        settings.ModifiedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    public async Task SeedDefaultsAsync()
    {
        if (!await _context.RoutingRules.AnyAsync())
        {
            foreach (var d in Defaults)
            {
                await _context.RoutingRules.AddAsync(new RoutingRule
                {
                    DocumentCategory = d.Category,
                    TargetModule = d.Module,
                    AllowAutoRouting = d.AutoRoute,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                });
            }
        }

        if (!await _context.AIConfidenceSettings.AnyAsync())
        {
            await _context.AIConfidenceSettings.AddAsync(new AIConfidenceSettings());
        }

        await _context.SaveChangesAsync();
    }
}
