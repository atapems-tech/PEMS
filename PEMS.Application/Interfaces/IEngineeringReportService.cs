using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// گزارش مدارک مهندسی/MDR - طبق درخواست: گزارش‌گیری از بخش‌های مختلف پروژه.
/// </summary>
public interface IEngineeringReportService
{
    Task<EngineeringReportDto> GetReportAsync(int? projectId = null, int? disciplineId = null,
        DateTime? fromDate = null, DateTime? toDate = null);
}
