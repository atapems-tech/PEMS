using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface ICmmsReportService
{
    Task<CmmsReportDto> GetReportAsync(string? status = null, string? priority = null,
        DateTime? fromDate = null, DateTime? toDate = null);
}
