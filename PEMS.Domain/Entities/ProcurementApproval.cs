using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// تاریخچه‌ی تایید یک Procurement Package (بخشی از "Approval History" که سند خواسته است).
/// </summary>
public class ProcurementApproval
{
    [Key]
    public int ProcurementApprovalId { get; set; }

    public int ProcurementPackageId { get; set; }
    public ProcurementPackage? ProcurementPackage { get; set; }

    public string ApproverName { get; set; } = string.Empty;

    // Approved, Rejected
    public string Decision { get; set; } = "Approved";

    public string? Comment { get; set; }

    public DateTime ApprovalDate { get; set; } = DateTime.Now;
}
