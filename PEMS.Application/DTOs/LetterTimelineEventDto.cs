namespace PEMS.Application.DTOs;

public class LetterTimelineEventDto
{
    public DateTime EventDate { get; set; }
    public string EventType { get; set; } = ""; // Created, Referred, Read, Completed, Recipient, Signed, Status
    public string Description { get; set; } = "";
    public string? ActorName { get; set; }
    public string Icon { get; set; } = "bi-circle";
    public string Color { get; set; } = "secondary";
}
