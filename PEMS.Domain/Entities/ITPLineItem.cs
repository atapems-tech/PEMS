using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// یک ردیف/فعالیت از ITP. InspectionCategory دقیقاً طبقه‌بندی استاندارد صنعتی است:
/// H (Hold) = بدون حضور و تایید بازرس نمی‌توان ادامه داد
/// W (Witness) = بازرس باید مطلع شود ولی عدم حضورش مانع ادامه نیست
/// R (Review) = فقط بازبینی مستندات، بدون حضور فیزیکی
/// S (Surveillance/Random) = بازرسی تصادفی/نمونه‌ای
/// M (Monitor) = فقط پایش، بدون تاثیر مستقیم روی توقف فعالیت
/// </summary>
public class ITPLineItem : BaseEntity
{
    public int ITPLineItemId { get; set; }

    public int InspectionTestPlanId { get; set; }
    public InspectionTestPlan? InspectionTestPlan { get; set; }

    public int SequenceNo { get; set; }

    public string Activity { get; set; } = string.Empty;

    public int QCDisciplineId { get; set; }
    public QCDiscipline? QCDiscipline { get; set; }

    // H, W, R, S, M
    public string InspectionCategory { get; set; } = "R";

    public string? AcceptanceCriteria { get; set; }

    public string? ReferenceCodeStandard { get; set; }

    // Contractor, Client, ThirdParty - طبق فرمت واقعی ITP که چند ستون مسئولیت دارد
    public string? ResponsibleParty { get; set; }

    // Pending, Notified, InProgress, Passed, Failed, Waived
    public string Status { get; set; } = "Pending";

    // ==============================================================================
    // اعلان Hold Point: طبق فاز بعدی - وقتی مسئول ذی‌ربط از رسیدن به این نقطه مطلع شد
    // (به‌خصوص برای H=Hold که بدون حضورش نمی‌توان ادامه داد)
    // ==============================================================================
    public DateTime? NotifiedDate { get; set; }
    public int? NotifiedById { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
