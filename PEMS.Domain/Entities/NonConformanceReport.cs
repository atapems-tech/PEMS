using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// Non-Conformance Report - قبلاً هیچ مفهومی از این در سیستم نبود. طبق ISO 9001 بند ۸.۷ و
/// رویه‌ی استاندارد QC صنعت: شناسایی → بررسی/Disposition → اقدام اصلاحی → بستن.
/// </summary>
public class NonConformanceReport : BaseEntity
{
    public int NonConformanceReportId { get; set; }

    public string NCRNumber { get; set; } = string.Empty;

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int QCDisciplineId { get; set; }
    public QCDiscipline? QCDiscipline { get; set; }

    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public int? ITPLineItemId { get; set; }
    public ITPLineItem? ITPLineItem { get; set; }

    public int? InspectionRecordId { get; set; }
    public InspectionRecord? InspectionRecord { get; set; }

    public string Description { get; set; } = string.Empty;

    // Minor, Major, Critical
    public string Severity { get; set; } = "Minor";

    // Pending, RepairAccept, ReworkToConform, UseAsIs, Reject, Scrap
    public string? Disposition { get; set; }

    public string? RootCause { get; set; }

    public string? CorrectiveAction { get; set; }

    public string? PreventiveAction { get; set; }

    // Open, UnderReview, Dispositioned, Closed
    public string Status { get; set; } = "Open";

    public int? RaisedById { get; set; }
    public DateTime RaisedDate { get; set; } = DateTime.Now;

    public int? DispositionedById { get; set; }
    public DateTime? DispositionedDate { get; set; }

    public int? ClosedById { get; set; }
    public DateTime? ClosedDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
