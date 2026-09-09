namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 6» بند ۱۸: نتیجه‌ی ارزیابی گیت ایمنی - هر دلیل صریح و قابل‌اقدام است
/// (بند ۸۴: «Cannot release Work Order. Reason: ...» نه یک خطای مبهم).
/// </summary>
public class WorkOrderReleaseGateResultDto
{
    public bool CanRelease { get; set; } = true;
    public List<string> Reasons { get; set; } = new();
}
