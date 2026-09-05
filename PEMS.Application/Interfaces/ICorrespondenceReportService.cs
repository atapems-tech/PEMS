using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// گزارش‌های مکاتبات (طبق نمونه‌ی کارتابل مرجع - بخش "گزارش‌ها") - فقط تحلیل روی داده‌ی
/// موجود Letter/LetterReferral/Tag، بدون هیچ Entity جدید.
/// </summary>
public interface ICorrespondenceReportService
{
    Task<CorrespondenceReportDto> GetReportAsync(int? projectId = null, int? disciplineId = null,
        DateTime? fromDate = null, DateTime? toDate = null);
}
