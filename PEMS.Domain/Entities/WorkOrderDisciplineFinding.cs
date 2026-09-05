namespace PEMS.Domain.Entities;

/// <summary>
/// یافته/نظر ثبت‌شده‌ی یک دیسیپلین خاص روی یک WorkOrder - طبق ISO 14224، هر دیسیپلین
/// می‌تواند حالت/علت خرابی را از دید تخصص خودش هم ثبت کند (مثلاً برق می‌گه ElectricalFault،
/// مکانیک می‌گه Vibration - هردو می‌تونن هم‌زمان درست باشن).
/// </summary>
public class WorkOrderDisciplineFinding
{
    public int WorkOrderDisciplineFindingId { get; set; }

    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public int DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    public int CycleNumber { get; set; } = 1;

    // Approve, Comment, Reject
    public string Decision { get; set; } = "Approve";

    public string? Findings { get; set; }

    // طبق ISO 14224: هر دیسیپلین می‌تونه از دید خودش حالت/علت خرابی رو ثبت کنه
    public string? FailureModeCode { get; set; }
    public string? FailureCauseCode { get; set; }

    public int? RespondedById { get; set; }
    public DateTime RespondedDate { get; set; } = DateTime.Now;
}
