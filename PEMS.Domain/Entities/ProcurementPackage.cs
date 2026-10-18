using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند Engineering Intelligence Redesign (بخش ۱): Entity والد یکپارچه که MR، MRQ،
/// Vendor Inquiry، Technical Clarification، Engineering Question، Response، تاریخچه‌ی بازبینی
/// و تایید را زیر یک سقف واحد جمع می‌کند - به‌جای دو سیستم جدای MR و MRQ.
/// جریان: Procurement Package → MR → Vendor Inquiry → Technical Clarification → MRQ →
/// Response → Approval → Closeout.
/// </summary>
public class ProcurementPackage : BaseEntity
{
    [Key]
    public int ProcurementPackageId { get; set; }

    public string PackageNumber { get; set; } = string.Empty;

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int? DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    // MR, VendorInquiry, TechnicalClarification, MRQ, Response, Approval, Closeout
    public string Stage { get; set; } = "MR";

    // Open, InProgress, Closed, Cancelled
    public string Status { get; set; } = "Open";

    // طبق ممیزی Package Procurement Contract Finalization (§C, §E): بحرانی‌بودن بسته مستقل از
    // Equipment Criticality است - همان واژگان EquipmentCriticality.CriticalityClass (A/B/C) و
    // RiskLevel (Low/Medium/High) برای یکپارچگی با بقیه‌ی سیستم استفاده شده، نه یک طبقه‌بندی تازه.
    public string? CriticalityClass { get; set; }
    public string? RiskLevel { get; set; }

    // آیا این بسته نیازمند کنترل‌های بازرسی الزامی است (ITP) - جزئیات پیوند در Phase 5 اضافه می‌شود
    public bool RequiresInspection { get; set; }
    public string? InspectionNotes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? ClosedDate { get; set; }

    public ICollection<MaterialRequisition> MaterialRequisitions { get; set; } = new List<MaterialRequisition>();
    public ICollection<MaterialRequisitionQuotation> Quotations { get; set; } = new List<MaterialRequisitionQuotation>();
    public ICollection<ProcurementCommunication> Communications { get; set; } = new List<ProcurementCommunication>();
    public ICollection<ProcurementApproval> Approvals { get; set; } = new List<ProcurementApproval>();
}
