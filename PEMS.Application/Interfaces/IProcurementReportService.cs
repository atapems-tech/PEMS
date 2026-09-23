using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface IProcurementReportService
{
    Task<ProcurementReportDto> GetReportAsync(int? projectId = null, int? disciplineId = null,
        DateTime? fromDate = null, DateTime? toDate = null);

    // طبق گزارش سخت‌افزاری نهایی (Gap ۵): جدول جداگانه و صفحه‌بندی‌شده - سنگین‌تر از خلاصه‌ی
    // بالا (چند Join)، پس در GetReportAsync ادغام نشده تا داشبورد اصلی کند نشود
    Task<PagedResult<ProcurementPipelineRowDto>> GetPipelineAsync(
        int? projectId = null, int? disciplineId = null, DateTime? fromDate = null, DateTime? toDate = null,
        int page = 1, int pageSize = 25);
}
