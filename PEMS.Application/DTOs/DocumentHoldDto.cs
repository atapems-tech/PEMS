namespace PEMS.Application.DTOs;

public class DocumentHoldDto
{
    public int DocumentHoldId { get; set; }
    public string Reason { get; set; } = "";
    public DateTime HoldDate { get; set; }
    public string? HoldByName { get; set; }
    public string? ResponsibleDisciplineName { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string? ReleaseByName { get; set; }
    public string? ReleaseReason { get; set; }
    public bool IsOpen => ReleaseDate == null;
}
