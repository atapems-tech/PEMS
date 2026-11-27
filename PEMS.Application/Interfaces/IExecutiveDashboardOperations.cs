using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// SCRUM-832: the JSON the executive dashboard bundle reads (served at /x/executive-dashboard-data), as an operation. Every section is filled only with the View right of its own module, and without the right to view all
/// disciplines the engineering numbers are limited to the discipline of the employee (the rules of the old controller).
/// </summary>
public interface IExecutiveDashboardOperations
{
    [PEMS.Application.Common.PemsOperation("ExecutiveDashboard", "Data")]
    Task<ExecutiveDashboardDto> DataAsync(int? projectId = null);
}
