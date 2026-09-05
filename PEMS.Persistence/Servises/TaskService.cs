using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class TaskService : ITaskService
{
    private readonly PEMSDbContext _context;
    private readonly INotificationService _notificationService;

    public TaskService(PEMSDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<int> CreateAsync(string title, string? description, int? assignedToEmployeeId,
        DateTime? dueDate, string priority, string? relatedType, int? relatedId,
        int? assignedByApplicationUserId, string? assignedByName)
    {
        var task = new TaskItem
        {
            Title = title,
            Description = description,
            AssignedToEmployeeId = assignedToEmployeeId,
            AssignedByApplicationUserId = assignedByApplicationUserId,
            AssignedByName = assignedByName,
            DueDate = dueDate,
            Priority = string.IsNullOrWhiteSpace(priority) ? "Normal" : priority,
            Status = "Open",
            RelatedType = relatedType,
            RelatedId = relatedId,
            CreatedDate = DateTime.Now
        };

        await _context.TaskItems.AddAsync(task);
        await _context.SaveChangesAsync();

        if (assignedToEmployeeId.HasValue)
        {
            var targetUserId = await _context.Users
                .Where(x => x.EmployeeId == assignedToEmployeeId.Value)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();

            if (targetUserId.HasValue)
            {
                await _notificationService.CreateAsync(
                    targetUserId.Value,
                    "وظیفه‌ی جدید به شما محول شد",
                    title,
                    "/Task/Index",
                    "TaskItem",
                    task.TaskItemId);
            }
        }

        return task.TaskItemId;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): قبلاً کل جدول فیلترشده با
    // ToListAsync (بدون Skip/Take) بارگذاری می‌شد.
    public async Task<PagedResult<TaskItemDto>> GetMyActionItemsAsync(int applicationUserId, bool openOnly = true, int page = 1, int pageSize = 50)
    {
        var employeeId = await _context.Users
            .Where(x => x.Id == applicationUserId)
            .Select(x => x.EmployeeId)
            .FirstOrDefaultAsync();

        if (!employeeId.HasValue) return new PagedResult<TaskItemDto> { Page = page, PageSize = pageSize };

        var query = _context.TaskItems.Where(x => x.AssignedToEmployeeId == employeeId.Value);

        if (openOnly) query = query.Where(x => x.Status != "Completed" && x.Status != "Cancelled");

        return await PageAsync(query, page, pageSize);
    }

    public async Task<PagedResult<TaskItemDto>> GetAssignedByMeAsync(int applicationUserId, bool openOnly = true, int page = 1, int pageSize = 50)
    {
        var query = _context.TaskItems.Where(x => x.AssignedByApplicationUserId == applicationUserId);

        if (openOnly) query = query.Where(x => x.Status != "Completed" && x.Status != "Cancelled");

        return await PageAsync(query, page, pageSize);
    }

    private async Task<PagedResult<TaskItemDto>> PageAsync(IQueryable<TaskItem> query, int page, int pageSize)
    {
        var totalCount = await query.CountAsync();

        page = page < 1 ? 1 : page;
        pageSize = DataGridPageSize.Normalize(pageSize);

        var tasks = await query
            .Include(x => x.AssignedToEmployee)
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.TaskItemId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<TaskItemDto> { Items = Map(tasks), Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<List<TaskItemDto>> GetForRelatedAsync(string relatedType, int relatedId)
    {
        var tasks = await _context.TaskItems
            .Include(x => x.AssignedToEmployee)
            .Where(x => x.RelatedType == relatedType && x.RelatedId == relatedId)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        return Map(tasks);
    }

    public async Task UpdateStatusAsync(int taskItemId, string status, string? completionNote = null)
    {
        var task = await _context.TaskItems.FirstOrDefaultAsync(x => x.TaskItemId == taskItemId);
        if (task == null) return;

        task.Status = status;

        if (status == "Completed")
        {
            task.CompletedDate = DateTime.Now;
            task.CompletionNote = completionNote;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<int> GetOpenActionItemCountAsync(int applicationUserId)
    {
        var employeeId = await _context.Users
            .Where(x => x.Id == applicationUserId)
            .Select(x => x.EmployeeId)
            .FirstOrDefaultAsync();

        if (!employeeId.HasValue) return 0;

        return await _context.TaskItems.CountAsync(x =>
            x.AssignedToEmployeeId == employeeId.Value && x.Status != "Completed" && x.Status != "Cancelled");
    }

    private static List<TaskItemDto> Map(List<TaskItem> tasks)
    {
        return tasks.Select(t => new TaskItemDto
        {
            TaskItemId = t.TaskItemId,
            Title = t.Title,
            Description = t.Description,
            AssignedToEmployeeName = t.AssignedToEmployee != null
                ? t.AssignedToEmployee.FirstName + " " + t.AssignedToEmployee.LastName
                : null,
            AssignedByName = t.AssignedByName,
            DueDate = t.DueDate,
            Priority = t.Priority,
            Status = t.Status,
            RelatedType = t.RelatedType,
            RelatedId = t.RelatedId,
            CreatedDate = t.CreatedDate,
            CompletedDate = t.CompletedDate,
            CompletionNote = t.CompletionNote,
            IsOverdue = t.Status != "Completed" && t.Status != "Cancelled"
                && t.DueDate.HasValue && t.DueDate.Value < DateTime.Now
        }).ToList();
    }
}
