using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// مدیریت جلسات (طبق نمونه‌ی کارتابل مرجع) - اقدامات پیگیری هر جلسه از همان ITaskService
/// موجود استفاده می‌کنند (RelatedType="Meeting")، نه یک سیستم Task جدا.
/// </summary>
public interface IMeetingService
{
    Task<List<MeetingDto>> GetAllAsync();

    Task<MeetingDetailDto?> GetDetailAsync(int meetingId);

    Task<int> CreateAsync(string title, DateTime meetingDate, string? location, string? organizerName,
        int? projectId, string? agenda);

    Task UpdateMinutesAsync(int meetingId, string minutesNotes, string status);

    Task AddAttendeeAsync(int meetingId, int? employeeId, string? externalName, bool isRequired);

    Task MarkAttendedAsync(int meetingAttendeeId, bool attended);
}
