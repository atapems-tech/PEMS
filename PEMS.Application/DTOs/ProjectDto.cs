namespace PEMS.Application.DTOs;

public class ProjectDto
{
    public int ProjectId { get; set; }
    public string ProjectCode { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string? ClientName { get; set; }
    public string? Location { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "Planning";
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }

    public int DocumentsCount { get; set; }
}
