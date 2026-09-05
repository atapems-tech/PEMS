using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

public class Contract : BaseEntity
{
    [Key]
    public int ContractId { get; set; }

    public string ContractNo { get; set; } = string.Empty;

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public string? ScopeOfWork { get; set; }

    public decimal? ContractValue { get; set; }

    public string? Currency { get; set; } = "USD";

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    // Draft, Active, Completed, Terminated
    public string Status { get; set; } = "Draft";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

    // طبق Phase E-04 (رفع P1 ممیزی Reverse Traceability) - سمت معکوس EngineeringDocument.ContractId
    public ICollection<EngineeringDocument> EngineeringDocuments { get; set; } = new List<EngineeringDocument>();
}
