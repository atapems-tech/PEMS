namespace PEMS.Application.DTOs;

public class ManufacturerDataBookDto
{
    public int MDBId { get; set; }

    public int EquipmentId { get; set; }
    public string EquipmentTagNumber { get; set; } = "";

    public int VendorId { get; set; }
    public string VendorName { get; set; } = "";

    public int? PoId { get; set; }
    public string? PoNumber { get; set; }

    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = "";

    public DateTime? PlannedFinalIssueDate { get; set; }
    public DateTime? IndexOfContentsDueDate { get; set; }
    public DateTime? IndexOfContentsSubmittedDate { get; set; }
    public bool IsIndexOverdue { get; set; }

    public string Status { get; set; } = "";

    public int? HardCopyCount { get; set; }
    public DateTime? HardCopyShippedDate { get; set; }
    public DateTime? SoftCopyDeliveredDate { get; set; }

    public string? Notes { get; set; }

    public List<MDBDocumentDto> DocumentsPartA { get; set; } = new();
    public List<MDBDocumentDto> DocumentsPartB { get; set; } = new();
    public List<MDBDocumentDto> DocumentsPartC { get; set; } = new();
}

public class MDBDocumentDto
{
    public int MDBDocumentId { get; set; }
    public string Part { get; set; } = "A";
    public int? DocumentId { get; set; }
    public string? DocumentNo { get; set; }
    public string Title { get; set; } = "";
    public int? DvdVolumeNo { get; set; }
    public bool IsNotApplicable { get; set; }
    public string? Notes { get; set; }
}
