namespace PEMS.Application.DTOs;

public class EngineeringDocumentDto
{

    public int DocumentId { get; set; }


    // =========================
    // Basic Information
    // =========================

    public string DocumentNo { get; set; } = "";

    public string Title { get; set; } = "";

    public string DocumentType { get; set; } = "";

    public string? Description { get; set; }



    // Project

    public int ProjectId { get; set; }

    public string ProjectName { get; set; } = "";



    // Discipline

    public int DisciplineId { get; set; }

    public string DisciplineName { get; set; } = "";


    // Traceability

    public int? EquipmentId { get; set; }

    public string? EquipmentTagNumber { get; set; }

    public int? ContractId { get; set; }

    public string? ContractNo { get; set; }



    // Status

    public string Status { get; set; } = "Draft";


    public string RevisionNo { get; set; } = "00";

    // فیلدهای MDR واقعی (طبق سند Development Specification، بخش ۳)
    public string? Area { get; set; }

    public string? CommentStatus { get; set; }

    public bool IsFrozen { get; set; }
    public DateTime? FrozenDate { get; set; }
    public string? FrozenByName { get; set; }
    public string? FreezeReason { get; set; }
    public DateTime? UnfrozenDate { get; set; }
    public string? UnfrozenByName { get; set; }
    public string? UnfreezeReason { get; set; }

    // طبق بخش M ممیزی (Hold Lifecycle)
    public bool IsOnHold { get; set; }

    public DateTime? PlannedDate { get; set; }

    public DateTime? ActualDate { get; set; }

    public int? DelayDays { get; set; }

    public DateTime CreatedDate { get; set; }



    // =========================
    // Engineering Information
    // =========================


    public string? IssuePurpose { get; set; }


    public string? PreparedBy { get; set; }


    public string? CheckedBy { get; set; }

    // طبق درخواست: امضای دیجیتال ترسیمی کارمند - در جاهایی که نمایش امضای تاییدکننده لازمه
    public string? CheckedBySignaturePath { get; set; }


    public string? ApprovedBy { get; set; }

    public string? ApprovedBySignaturePath { get; set; }

    public DateTime? CheckedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }

    // تایید کارفرما جدا از تایید داخلی (طبق بررسی چرخه‌ی مدرک)
    public string? ClientApprovalStatus { get; set; }
    public string? ClientComment { get; set; }
    public DateTime? ClientResponseDate { get; set; }




    // =========================
    // Attachments
    // =========================


    public List<DocumentAttachmentDto> Attachments { get; set; }
        = new();




    // =========================
    // Revision History
    // =========================


    public List<DocumentRevisionDto> Revisions { get; set; }
        = new();




    // =========================
    // Workflow History
    // =========================


    public List<DocumentWorkflowDto> Workflows { get; set; }
        = new();

}