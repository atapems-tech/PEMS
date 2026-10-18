using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class NotificationService : INotificationService
{
    private readonly PEMSDbContext _context;
    private readonly INotificationBroadcaster _broadcaster;

    public NotificationService(PEMSDbContext context, INotificationBroadcaster broadcaster)
    {
        _context = context;
        _broadcaster = broadcaster;
    }

    private IQueryable<Notification> Active(int userId) =>
        _context.Notifications.Where(x => x.UserId == userId && x.DismissedDate == null);

    private static NotificationDto Map(Notification x) => new()
    {
        NotificationId = x.NotificationId,
        Title = x.Title,
        Message = x.Message,
        LinkUrl = x.LinkUrl,
        RelatedType = x.RelatedType,
        RelatedId = x.RelatedId,
        IsRead = x.IsRead,
        DismissedDate = x.DismissedDate,
        CreatedDate = x.CreatedDate
    };

    public async Task CreateAsync(int userId, string title, string message, string? linkUrl = null,
        string? relatedType = null, int? relatedId = null)
    {
        var entity = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            LinkUrl = linkUrl,
            RelatedType = relatedType,
            RelatedId = relatedId,
            IsRead = false,
            CreatedDate = DateTime.Now
        };

        await _context.Notifications.AddAsync(entity);
        await _context.SaveChangesAsync();

        // اگر SignalR در دسترس نباشد (کاربر آفلاین)، اعلان در دیتابیس ثبت شده و با باز کردن بعدی
        // صفحه (شمارنده‌ی مرجع سمت سرور) دیده می‌شود.
        try
        {
            var unread = await GetUnreadCountAsync(userId);
            await _broadcaster.BroadcastNotificationAsync(userId, Map(entity), unread);
        }
        catch
        {
            // پخش زنده اختیاری است - شکستش نباید ثبت اعلان را خراب کند
        }
    }

    public async Task<List<NotificationDto>> GetForUserAsync(int userId, bool unreadOnly = false)
    {
        var query = Active(userId);

        if (unreadOnly)
            query = query.Where(x => !x.IsRead);

        return await query
            .OrderByDescending(x => x.CreatedDate)
            .Take(50)
            .Select(x => new NotificationDto
            {
                NotificationId = x.NotificationId,
                Title = x.Title,
                Message = x.Message,
                LinkUrl = x.LinkUrl,
                RelatedType = x.RelatedType,
                RelatedId = x.RelatedId,
                IsRead = x.IsRead,
                DismissedDate = x.DismissedDate,
                CreatedDate = x.CreatedDate
            })
            .ToListAsync();
    }

    public async Task<PagedResult<NotificationDto>> GetInboxAsync(int userId, string? filter, int page, int pageSize)
    {
        IQueryable<Notification> query = (filter ?? "all").ToLowerInvariant() switch
        {
            "unread" => Active(userId).Where(x => !x.IsRead),
            "read" => Active(userId).Where(x => x.IsRead),
            "dismissed" => _context.Notifications.Where(x => x.UserId == userId && x.DismissedDate != null),
            _ => Active(userId)
        };

        page = page < 1 ? 1 : page;
        pageSize = DataGridPageSize.Normalize(pageSize);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.NotificationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new NotificationDto
            {
                NotificationId = x.NotificationId,
                Title = x.Title,
                Message = x.Message,
                LinkUrl = x.LinkUrl,
                RelatedType = x.RelatedType,
                RelatedId = x.RelatedId,
                IsRead = x.IsRead,
                DismissedDate = x.DismissedDate,
                CreatedDate = x.CreatedDate
            })
            .ToListAsync();

        return new PagedResult<NotificationDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await Active(userId).CountAsync(x => !x.IsRead);
    }

    public async Task<bool> MarkAsReadAsync(int userId, int notificationId)
    {
        var n = await _context.Notifications
            .FirstOrDefaultAsync(x => x.NotificationId == notificationId && x.UserId == userId);
        if (n == null) return false;

        if (!n.IsRead)
        {
            n.IsRead = true;
            n.ReadDate = DateTime.Now;
            await _context.SaveChangesAsync();
            await SyncCountAsync(userId);
        }

        return true;
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        var items = await Active(userId).Where(x => !x.IsRead).ToListAsync();
        if (items.Count == 0) return;

        var now = DateTime.Now;
        foreach (var n in items)
        {
            n.IsRead = true;
            n.ReadDate = now;
        }

        await _context.SaveChangesAsync();
        await SyncCountAsync(userId);
    }

    public async Task<string?> OpenAsync(int userId, int notificationId)
    {
        var n = await _context.Notifications
            .FirstOrDefaultAsync(x => x.NotificationId == notificationId && x.UserId == userId);
        if (n == null) return null;

        if (!n.IsRead)
        {
            n.IsRead = true;
            n.ReadDate = DateTime.Now;
            await _context.SaveChangesAsync();
            await SyncCountAsync(userId);
        }

        return n.LinkUrl;
    }

    public async Task<bool> DismissAsync(int userId, int notificationId)
    {
        var n = await _context.Notifications
            .FirstOrDefaultAsync(x => x.NotificationId == notificationId && x.UserId == userId);
        if (n == null) return false;

        if (n.DismissedDate == null)
        {
            var now = DateTime.Now;
            if (!n.IsRead) { n.IsRead = true; n.ReadDate = now; }
            n.DismissedDate = now;
            await _context.SaveChangesAsync();
            await SyncCountAsync(userId);
        }

        return true;
    }

    public async Task DismissAllAsync(int userId)
    {
        var items = await Active(userId).ToListAsync();
        if (items.Count == 0) return;

        var now = DateTime.Now;
        foreach (var n in items)
        {
            if (!n.IsRead) { n.IsRead = true; n.ReadDate = now; }
            n.DismissedDate = now;
        }

        await _context.SaveChangesAsync();
        await SyncCountAsync(userId);
    }

    public async Task MarkRelatedReadAsync(int userId, string relatedType, int relatedId)
    {
        var items = await Active(userId)
            .Where(x => !x.IsRead && x.RelatedType == relatedType && x.RelatedId == relatedId)
            .ToListAsync();
        if (items.Count == 0) return;

        var now = DateTime.Now;
        foreach (var n in items) { n.IsRead = true; n.ReadDate = now; }

        await _context.SaveChangesAsync();
        await SyncCountAsync(userId);
    }

    public async Task ResolveRelatedAsync(int? userId, string relatedType, int relatedId)
    {
        var query = _context.Notifications.Where(x =>
            x.DismissedDate == null && x.RelatedType == relatedType && x.RelatedId == relatedId);
        if (userId.HasValue) query = query.Where(x => x.UserId == userId.Value);

        var items = await query.ToListAsync();
        if (items.Count == 0) return;

        var now = DateTime.Now;
        foreach (var n in items)
        {
            if (!n.IsRead) { n.IsRead = true; n.ReadDate = now; }
            n.DismissedDate = now;
        }

        await _context.SaveChangesAsync();

        foreach (var uid in items.Select(x => x.UserId).Distinct())
            await SyncCountAsync(uid);
    }

    private async Task SyncCountAsync(int userId)
    {
        try
        {
            await _broadcaster.BroadcastUnreadCountAsync(userId, await GetUnreadCountAsync(userId));
        }
        catch
        {
            // همگام‌سازی زنده اختیاری است؛ مرجع، شمارنده‌ی سمت سرور در بارگذاری بعدی است
        }
    }
}
