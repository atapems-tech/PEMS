namespace PEMS.Application.DTOs;

public class MeetingDto
{
    public int MeetingId { get; set; }
    public string Title { get; set; } = "";
    public DateTime MeetingDate { get; set; }
    public string? Location { get; set; }
    public string? OrganizerName { get; set; }
    public string? ProjectName { get; set; }
    public string Status { get; set; } = "Scheduled";
    public int AttendeeCount { get; set; }
    public int ActionItemCount { get; set; }
}

public class MeetingAttendeeDto
{
    public int MeetingAttendeeId { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsRequired { get; set; }
    public bool Attended { get; set; }
}

public class MeetingDetailDto
{
    public MeetingDto Meeting { get; set; } = new();
    public string? Agenda { get; set; }
    public string? MinutesNotes { get; set; }
    public List<MeetingAttendeeDto> Attendees { get; set; } = new();
    public List<TaskItemDto> ActionItems { get; set; } = new();
}
