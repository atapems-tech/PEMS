using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۶. عمداً هیچ متد «PredictAsync» ای که یک عدد تولید کند این‌جا وجود
/// ندارد - چون هیچ مدل آموزش‌دیده/اعتبارسنجی‌شده‌ی واقعی‌ای پشت آن نیست. اضافه‌کردن چنین متدی
/// بدون داده‌ی اعتبارسنجی واقعی، دقیقاً همان «Do not train meaningless models ... and call them
/// validated» است که بند ۱۶ صریحاً منع کرده.
/// </summary>
public interface IPredictiveMaintenanceService
{
    Task<PredictiveMaintenanceReadinessDto> GetReadinessAsync();

    Task<List<PredictiveMaintenanceModelDto>> GetModelsAsync();
}
