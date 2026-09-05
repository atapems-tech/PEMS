using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// درخواست خرید متریال/تجهیز مهندسی (Material Requisition).
/// </summary>
public class MaterialRequisition : BaseEntity
{
    [Key]
    public int MRId { get; set; }

    public string MRNumber { get; set; } = string.Empty;

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    // طبق سند Engineering Intelligence Redesign: MR حالا زیرمجموعه‌ی یک Procurement Package است
    public int? ProcurementPackageId { get; set; }
    public ProcurementPackage? ProcurementPackage { get; set; }

    public int DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    // شماره تگ تجهیز مرتبط (اختیاری، آزاد به‌صورت متن برای سادگی)
    public string? EquipmentTagNumber { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTime? RequiredDate { get; set; }

    public int? ResponsibleEngineerId { get; set; }
    public Employee? ResponsibleEngineer { get; set; }

    // Draft, Submitted, Under Technical Review, Approved, Issued to Procurement, Closed, Cancelled
    public string Status { get; set; } = "Draft";

    // مدرک مهندسی مرتبط (دیتاشیت/اسپک) - اختیاری
    public int? RelatedDocumentId { get; set; }
    public EngineeringDocument? RelatedDocument { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<MaterialRequisitionItem> Items { get; set; } = new List<MaterialRequisitionItem>();

    public ICollection<MaterialRequisitionQuotation> Quotations { get; set; } = new List<MaterialRequisitionQuotation>();

    // طبق Phase E-04 (رفع P1 ممیزی Reverse Traceability): مدارک مهندسی که این MR را به‌عنوان
    // "RelatedMR" علامت زده‌اند - سمت معکوس EngineeringDocument.RelatedMRId. جدا از
    // RelatedDocument بالا (که برعکسش است: این MR یک مدرک مشخص را به‌عنوان اسپک معرفی می‌کند).
    public ICollection<EngineeringDocument> EngineeringDocuments { get; set; } = new List<EngineeringDocument>();
}
