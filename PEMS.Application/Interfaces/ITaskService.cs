using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// وظیفه‌ی مستقل و عمومی (طبق نمونه‌ی کارتابل مرجع: "اقدام وظایف" / "پیگیری وظایف") -
/// می‌تواند مستقل باشد یا اختیاری به هر موجودیت دیگری (نامه، مدرک، VDR و ...) وصل شود.
/// </summary>
public interface ITaskService
{
    Task<int> CreateAsync(string title, string? description, int? assignedToEmployeeId,
        DateTime? dueDate, string priority, string? relatedType, int? relatedId,
        int? assignedByApplicationUserId, string? assignedByName);

    // "اقدام وظایف": وظایفی که باید کاربر جاری روی آن‌ها اقدام کند
    Task<PagedResult<TaskItemDto>> GetMyActionItemsAsync(int applicationUserId, bool openOnly = true, int page = 1, int pageSize = 50);

    // "پیگیری وظایف": وظایفی که کاربر جاری محول کرده و پیگیری می‌کند
    Task<PagedResult<TaskItemDto>> GetAssignedByMeAsync(int applicationUserId, bool openOnly = true, int page = 1, int pageSize = 50);

    Task<List<TaskItemDto>> GetForRelatedAsync(string relatedType, int relatedId);

    Task UpdateStatusAsync(int taskItemId, string status, string? completionNote = null);

    Task<int> GetOpenActionItemCountAsync(int applicationUserId);
}
