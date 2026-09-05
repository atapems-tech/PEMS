using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// درخواست استعلام قیمت مرتبط با یک MR (Material Requisition For Quotation).
/// </summary>
public class MaterialRequisitionQuotation
{
    [Key]
    public int MRQId { get; set; }

    public string MRQNumber { get; set; } = string.Empty;

    public int MRId { get; set; }
    public MaterialRequisition? MR { get; set; }

    // طبق سند Engineering Intelligence Redesign: گزارش‌دهی یکپارچه در سطح Procurement Package
    public int? ProcurementPackageId { get; set; }
    public ProcurementPackage? ProcurementPackage { get; set; }

    public string VendorName { get; set; } = string.Empty;

    public DateTime? InquiryDate { get; set; }

    public decimal? QuotedAmount { get; set; }

    public string? Currency { get; set; } = "USD";

    // Not Sent, Sent, Received, Under Evaluation, Evaluated
    public string QuotationStatus { get; set; } = "Not Sent";

    // Pending, Passed, Failed
    public string TechnicalEvaluation { get; set; } = "Pending";

    // Pending, Passed, Failed
    public string CommercialEvaluation { get; set; } = "Pending";

    // Pending, Approved, Rejected
    public string ApprovalStatus { get; set; } = "Pending";

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
