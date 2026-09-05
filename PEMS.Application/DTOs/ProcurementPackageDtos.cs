namespace PEMS.Application.DTOs;

public class ProcurementPackageDto
{
    public int ProcurementPackageId { get; set; }
    public string PackageNumber { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string? DisciplineName { get; set; }
    public string Title { get; set; } = "";
    public string Stage { get; set; } = "MR";
    public string Status { get; set; } = "Open";
    public int MRCount { get; set; }
    public int MRQCount { get; set; }
    public int OpenCommunicationsCount { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class ProcurementCommunicationDto
{
    public int ProcurementCommunicationId { get; set; }
    public string Type { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string? VendorName { get; set; }
    public string? RaisedByName { get; set; }
    public DateTime RaisedDate { get; set; }
    public string ActionStatus { get; set; } = "Open";
    public string? ResponseText { get; set; }
    public string? RespondedByName { get; set; }
    public DateTime? ResponseDate { get; set; }
    public int? AttachmentId { get; set; }
}

public class ProcurementApprovalDto
{
    public int ProcurementApprovalId { get; set; }
    public string ApproverName { get; set; } = "";
    public string Decision { get; set; } = "";
    public string? Comment { get; set; }
    public DateTime ApprovalDate { get; set; }
}

/// <summary>
/// نمای کامل یک Procurement Package - MR/MRQ های آن + کل تاریخچه‌ی ارتباطات و تاییدها،
/// دقیقاً همان "single screen" که سند خواسته است.
/// </summary>
public class ProcurementPackageDetailDto
{
    public ProcurementPackageDto Summary { get; set; } = new();
    public List<MRDto> MaterialRequisitions { get; set; } = new();
    public List<MRQDto> Quotations { get; set; } = new();
    public List<ProcurementCommunicationDto> Communications { get; set; } = new();
    public List<ProcurementApprovalDto> Approvals { get; set; } = new();
}
