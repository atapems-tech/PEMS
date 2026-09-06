namespace PEMS.Application.DTOs;

public class DocumentWorkflowDto
{

    public int WorkflowId { get; set; }

    // طبق «PEMS — Global Grid Hardening»: برای DocumentWorkflowController.Index (تاریخچه‌ی
    // سراسری چند-سندی) لازم است - در EngineeringDocuments Details (تک-سندی) خالی می‌ماند.
    public string? DocumentNo { get; set; }

    public string? FromStatus { get; set; }


    public string? ToStatus { get; set; }


    public string? ActionType { get; set; }


    public string? Comment { get; set; }


    public DateTime ActionDate { get; set; }


    public string? ActionBy { get; set; }

}

// طبق «PEMS — Global Grid Hardening» P1: ردیف فهرست صف تایید سراسری DocumentWorkflowController -
// جایگزین Model = List&lt;EngineeringDocument&gt; کامل با یک Projection سبک.
public class DocumentWorkflowPendingItemDto
{
    public int DocumentId { get; set; }
    public string DocumentNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string? ProjectName { get; set; }
    public string? DisciplineName { get; set; }
    public string Status { get; set; } = "";
    public string RevisionNo { get; set; } = "";
}