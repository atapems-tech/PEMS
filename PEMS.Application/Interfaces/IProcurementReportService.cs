using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface IProcurementReportService
{
    Task<ProcurementReportDto> GetReportAsync(int? projectId = null, int? disciplineId = null,
        DateTime? fromDate = null, DateTime? toDate = null);
}
