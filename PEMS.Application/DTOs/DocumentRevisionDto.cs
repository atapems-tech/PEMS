namespace PEMS.Application.DTOs;

public class DocumentRevisionDto
{
    public int RevisionId { get; set; }


    public string RevisionNo { get; set; } = "00";


    public string Status { get; set; } = "Draft";


    public string? Comment { get; set; }


    public DateTime RevisionDate { get; set; }


    public string PreparedBy { get; set; } = "";


    public string CheckedBy { get; set; } = "";


    public string ApprovedBy { get; set; } = "";

    public string? Reason { get; set; }
    public string? ChangeDescription { get; set; }
    public string? VendorComment { get; set; }
    public string? ReviewerName { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public string? SupersededRevisionNo { get; set; }
    public string? AIRevisionSummary { get; set; }
}