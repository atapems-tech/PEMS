namespace PEMS.Application.DTOs;

public class DocumentWorkflowDto
{

    public int WorkflowId { get; set; }


    public string? FromStatus { get; set; }


    public string? ToStatus { get; set; }


    public string? ActionType { get; set; }


    public string? Comment { get; set; }


    public DateTime ActionDate { get; set; }


    public string? ActionBy { get; set; }

}