using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// گزارش VDR - طبق درخواست: گزارش‌گیری از بخش‌های مختلف پروژه.
/// </summary>
public interface IVDRReportService
{
    Task<VDRReportDto> GetReportAsync(int? projectId = null, int? disciplineId = null,
        DateTime? fromDate = null, DateTime? toDate = null);
}
