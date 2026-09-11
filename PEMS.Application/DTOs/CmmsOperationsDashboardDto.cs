namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 8 — Operational Maintenance &amp; Reliability Data Activation» بند ۲۱:
/// در ممیزی مشخص شد که هیچ نمای واحدی شمارنده‌های وضعیت WorkRequest و WorkOrder را کنار هم
/// (به‌همراه معیار Overdue) نشان نمی‌دهد - KpiController فقط هزینه/Maintenance KPI دارد و
/// WorkOrderController.Index فقط چهار شمارنده‌ی محدود (Open/InProgress/Completed/Critical) دارد.
/// این DTO فقط تجمیع (COUNT) از داده‌ی واقعی موجود است - هیچ عددی جعل/تخمین زده نمی‌شود؛
/// همه‌ی شمارنده‌ها روی هم‌ان Scope فعال (IsActive) و هم‌ان مقادیر Status واقعی موجود در دیتابیس
/// محاسبه می‌شوند. PM Compliance و Corrective/Preventive/Emergency Breakdown از
/// IReliabilityIntelligenceService (فاز ۷) بازخوانی می‌شوند - دوباره محاسبه نمی‌شوند.
/// </summary>
public class CmmsOperationsDashboardDto
{
    // ---- Work Request ----
    public int WorkRequestDraft { get; set; }
    public int WorkRequestSubmitted { get; set; }
    public int WorkRequestUnderReview { get; set; }
    public int WorkRequestApproved { get; set; }
    public int WorkRequestPlanned { get; set; }
    public int WorkRequestConverted { get; set; }
    public int WorkRequestRejected { get; set; }
    public int WorkRequestCancelled { get; set; }
    public int WorkRequestTotal { get; set; }

    // مجموع درخواست‌هایی که هنوز نیاز به یک اقدام انسانی بعدی دارند (Submitted/UnderReview/Approved) -
    // Draft هنوز ارسال نشده و Planned منتظر تبدیل دستی به WorkOrder است، نه «در انتظار تایید».
    public int WorkRequestPendingAction { get; set; }

    // ---- Work Order ----
    public int WorkOrderOpen { get; set; }
    public int WorkOrderAssigned { get; set; }
    public int WorkOrderInProgress { get; set; }
    public int WorkOrderOnHold { get; set; }
    public int WorkOrderCompleted { get; set; }
    public int WorkOrderClosed { get; set; }
    public int WorkOrderCancelled { get; set; }
    public int WorkOrderRejected { get; set; }
    public int WorkOrderTotal { get; set; }

    // DueDate < امروز و هنوز در یکی از وضعیت‌های غیرنهایی (Open/Assigned/InProgress/OnHold) -
    // هرگز از مدت‌زمان WorkOrder استنتاج نمی‌شود، فقط از فیلد صریح DueDate.
    public int WorkOrderOverdue { get; set; }

    public int WorkOrderCriticalOpen { get; set; }

    // ---- Reuse از فاز ۷ (IReliabilityIntelligenceService) - بدون محاسبه‌ی موازی ----
    public PmComplianceDto? PmCompliance { get; set; }
    public MaintenanceTypeBreakdownDto? MaintenanceTypeBreakdown { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.Now;
}
