using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class MeetingAttendee
{
    [Key]
    public int MeetingAttendeeId { get; set; }

    public int MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public string? ExternalName { get; set; }

    public bool IsRequired { get; set; } = true;

    public bool Attended { get; set; }
}
