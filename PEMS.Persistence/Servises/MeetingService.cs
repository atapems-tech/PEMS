using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class MeetingService : IMeetingService
{
    private readonly PEMSDbContext _context;
    private readonly ITaskService _taskService;

    public MeetingService(PEMSDbContext context, ITaskService taskService)
    {
        _context = context;
        _taskService = taskService;
    }

    public async Task<List<MeetingDto>> GetAllAsync()
    {
        var meetings = await _context.Meetings
            .Include(x => x.Project)
            .Include(x => x.Attendees)
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.MeetingDate)
            .ToListAsync();

        var meetingIds = meetings.Select(x => x.MeetingId).ToList();

        var actionCounts = await _context.TaskItems
            .Where(x => x.RelatedType == "Meeting" && x.RelatedId.HasValue && meetingIds.Contains(x.RelatedId.Value))
            .GroupBy(x => x.RelatedId)
            .Select(g => new { MeetingId = g.Key, Count = g.Count() })
            .ToListAsync();

        return meetings.Select(m => new MeetingDto
        {
            MeetingId = m.MeetingId,
            Title = m.Title,
            MeetingDate = m.MeetingDate,
            Location = m.Location,
            OrganizerName = m.OrganizerName,
            ProjectName = m.Project?.ProjectName,
            Status = m.Status,
            AttendeeCount = m.Attendees.Count,
            ActionItemCount = actionCounts.FirstOrDefault(x => x.MeetingId == m.MeetingId)?.Count ?? 0
        }).ToList();
    }

    public async Task<MeetingDetailDto?> GetDetailAsync(int meetingId)
    {
        var meeting = await _context.Meetings
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.MeetingId == meetingId);

        if (meeting == null) return null;

        var attendees = await _context.MeetingAttendees
            .Include(x => x.Employee)
            .Where(x => x.MeetingId == meetingId)
            .ToListAsync();

        var actionItems = await _taskService.GetForRelatedAsync("Meeting", meetingId);

        return new MeetingDetailDto
        {
            Meeting = new MeetingDto
            {
                MeetingId = meeting.MeetingId,
                Title = meeting.Title,
                MeetingDate = meeting.MeetingDate,
                Location = meeting.Location,
                OrganizerName = meeting.OrganizerName,
                ProjectName = meeting.Project?.ProjectName,
                Status = meeting.Status,
                AttendeeCount = attendees.Count,
                ActionItemCount = actionItems.Count
            },
            Agenda = meeting.Agenda,
            MinutesNotes = meeting.MinutesNotes,
            Attendees = attendees.Select(a => new MeetingAttendeeDto
            {
                MeetingAttendeeId = a.MeetingAttendeeId,
                DisplayName = a.Employee != null ? a.Employee.FirstName + " " + a.Employee.LastName : (a.ExternalName ?? "-"),
                IsRequired = a.IsRequired,
                Attended = a.Attended
            }).ToList(),
            ActionItems = actionItems
        };
    }

    public async Task<int> CreateAsync(string title, DateTime meetingDate, string? location, string? organizerName,
        int? projectId, string? agenda)
    {
        var meeting = new Meeting
        {
            Title = title,
            MeetingDate = meetingDate,
            Location = location,
            OrganizerName = organizerName,
            ProjectId = projectId,
            Agenda = agenda,
            Status = "Scheduled",
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.Meetings.AddAsync(meeting);
        await _context.SaveChangesAsync();

        return meeting.MeetingId;
    }

    public async Task UpdateMinutesAsync(int meetingId, string minutesNotes, string status)
    {
        var meeting = await _context.Meetings.FirstOrDefaultAsync(x => x.MeetingId == meetingId);
        if (meeting == null) return;

        meeting.MinutesNotes = minutesNotes;
        meeting.Status = status;

        await _context.SaveChangesAsync();
    }

    public async Task AddAttendeeAsync(int meetingId, int? employeeId, string? externalName, bool isRequired)
    {
        await _context.MeetingAttendees.AddAsync(new MeetingAttendee
        {
            MeetingId = meetingId,
            EmployeeId = employeeId,
            ExternalName = externalName,
            IsRequired = isRequired,
            Attended = false
        });

        await _context.SaveChangesAsync();
    }

    public async Task MarkAttendedAsync(int meetingAttendeeId, bool attended)
    {
        var attendee = await _context.MeetingAttendees.FirstOrDefaultAsync(x => x.MeetingAttendeeId == meetingAttendeeId);
        if (attendee == null) return;

        attendee.Attended = attended;
        await _context.SaveChangesAsync();
    }
}
