using Microsoft.EntityFrameworkCore;
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

    public async Task CreateAsync(int userId, string title, string message, string? linkUrl = null,
        string? relatedType = null, int? relatedId = null)
    {
        await _context.Notifications.AddAsync(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            LinkUrl = linkUrl,
            RelatedType = relatedType,
            RelatedId = relatedId,
            IsRead = false,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        // طبق درخواست: اعلان لحظه‌ای بدون Refresh - اگر SignalR در دسترس نباشد (کاربر آفلاین)،
        // اعلان همچنان در دیتابیس ثبت شده و با باز کردن بعدی صفحه دیده می‌شود
        try
        {
            await _broadcaster.BroadcastToUserAsync(userId, title, linkUrl);
        }
        catch
        {
            // پخش زنده اختیاری است - شکستش نباید ثبت اعلان را خراب کند
        }
    }

    public async Task<List<NotificationDto>> GetForUserAsync(int userId, bool unreadOnly = false)
    {
        var query = _context.Notifications.Where(x => x.UserId == userId);

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
                IsRead = x.IsRead,
                CreatedDate = x.CreatedDate
            })
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _context.Notifications.CountAsync(x => x.UserId == userId && !x.IsRead);
    }

    public async Task MarkAsReadAsync(int notificationId)
    {
        var n = await _context.Notifications.FirstOrDefaultAsync(x => x.NotificationId == notificationId);
        if (n == null) return;

        n.IsRead = true;
        n.ReadDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        var items = await _context.Notifications.Where(x => x.UserId == userId && !x.IsRead).ToListAsync();

        foreach (var n in items)
        {
            n.IsRead = true;
            n.ReadDate = DateTime.Now;
        }

        await _context.SaveChangesAsync();
    }
}
