using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class UserDashboardService : IUserDashboardService
{
    private readonly PEMSDbContext _context;

    // کاتالوگ ثابت Widget های موجود در سیستم؛ افزودن نوع جدید فقط یعنی افزودن یک سطر این‌جا
    // و پیاده‌سازی رندر آن در View - بدون تغییر در مدل داده
    private static readonly (string Key, string Name)[] Catalog =
    {
        ("PendingDocuments", "Pending Engineering Documents"),
        ("MyReferrals", "My Correspondence Referrals"),
        ("OverdueLetters", "Overdue Correspondence"),
        ("RecentDocuments", "Recently Created Documents"),
        ("VDRSummary", "Vendor Document Register Summary"),
        ("IntakeSummary", "AI Intake Queue Summary"),
        ("MyDisciplineTasks", "My Discipline Tasks"),
        ("KnowledgeBaseShortcut", "Knowledge Base Quick Search"),
    };

    public UserDashboardService(PEMSDbContext context)
    {
        _context = context;
    }

    public List<UserDashboardWidgetDto> GetCatalog()
    {
        return Catalog.Select((c, i) => new UserDashboardWidgetDto
        {
            WidgetKey = c.Key,
            DisplayName = c.Name,
            DisplayOrder = i,
            Size = "Medium",
            IsVisible = false
        }).ToList();
    }

    public async Task<List<UserDashboardWidgetDto>> GetLayoutAsync(int applicationUserId)
    {
        var saved = await _context.UserDashboardWidgets
            .Where(x => x.ApplicationUserId == applicationUserId)
            .ToListAsync();

        if (!saved.Any())
        {
            // چیدمان پیش‌فرض منطقی برای کاربر تازه: چهار Widget اول کاتالوگ فعال باشند
            return Catalog.Select((c, i) => new UserDashboardWidgetDto
            {
                WidgetKey = c.Key,
                DisplayName = c.Name,
                DisplayOrder = i,
                Size = "Medium",
                IsVisible = i < 4
            }).ToList();
        }

        return Catalog.Select(c =>
        {
            var s = saved.FirstOrDefault(x => x.WidgetKey == c.Key);

            return new UserDashboardWidgetDto
            {
                WidgetKey = c.Key,
                DisplayName = c.Name,
                DisplayOrder = s?.DisplayOrder ?? 99,
                Size = s?.Size ?? "Medium",
                IsVisible = s?.IsVisible ?? false
            };
        }).OrderBy(x => x.DisplayOrder).ToList();
    }

    public async Task SaveLayoutAsync(int applicationUserId, List<UserDashboardWidgetDto> widgets)
    {
        var existing = await _context.UserDashboardWidgets
            .Where(x => x.ApplicationUserId == applicationUserId)
            .ToListAsync();

        foreach (var w in widgets)
        {
            var row = existing.FirstOrDefault(x => x.WidgetKey == w.WidgetKey);

            if (row == null)
            {
                row = new UserDashboardWidget { ApplicationUserId = applicationUserId, WidgetKey = w.WidgetKey };
                await _context.UserDashboardWidgets.AddAsync(row);
            }

            row.DisplayOrder = w.DisplayOrder;
            row.Size = w.Size;
            row.IsVisible = w.IsVisible;
        }

        await _context.SaveChangesAsync();
    }

    public async Task ResetToDefaultAsync(int applicationUserId)
    {
        var existing = await _context.UserDashboardWidgets
            .Where(x => x.ApplicationUserId == applicationUserId)
            .ToListAsync();

        _context.UserDashboardWidgets.RemoveRange(existing);
        await _context.SaveChangesAsync();
    }
}
