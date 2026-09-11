using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 8» بند ۲۱/۲۲. همه‌ی شمارنده‌ها SQL-Aggregated (GroupBy/CountAsync) هستند -
/// هیچ ToListAsync بدون فیلتر روی کل جدول انجام نمی‌شود.
/// </summary>
public class CmmsOperationsService : ICmmsOperationsService
{
    // طبق WorkOrderLifecycleService/PMGenerationService: وضعیت‌های غیرنهایی که هنوز روی Backlog
    // هستند - Completed/Closed/Cancelled/Rejected از Backlog خارج می‌شوند.
    private static readonly string[] OpenWorkOrderStatuses = { "Open", "Assigned", "InProgress", "OnHold" };

    private readonly PEMSDbContext _context;
    private readonly IReliabilityIntelligenceService _intelligenceService;

    public CmmsOperationsService(PEMSDbContext context, IReliabilityIntelligenceService intelligenceService)
    {
        _context = context;
        _intelligenceService = intelligenceService;
    }

    public async Task<CmmsOperationsDashboardDto> GetDashboardAsync()
    {
        var dto = new CmmsOperationsDashboardDto();

        var wrCounts = await _context.WorkRequests
            .Where(x => x.IsActive)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int WrCount(string status) => wrCounts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        dto.WorkRequestDraft = WrCount("Draft");
        dto.WorkRequestSubmitted = WrCount("Submitted");
        dto.WorkRequestUnderReview = WrCount("UnderReview");
        dto.WorkRequestApproved = WrCount("Approved");
        dto.WorkRequestPlanned = WrCount("Planned");
        dto.WorkRequestConverted = WrCount("Converted");
        dto.WorkRequestRejected = WrCount("Rejected");
        dto.WorkRequestCancelled = WrCount("Cancelled");
        dto.WorkRequestTotal = wrCounts.Sum(x => x.Count);
        dto.WorkRequestPendingAction = dto.WorkRequestSubmitted + dto.WorkRequestUnderReview + dto.WorkRequestApproved;

        var woCounts = await _context.WorkOrders
            .Where(x => x.IsActive)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int WoCount(string status) => woCounts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        dto.WorkOrderOpen = WoCount("Open");
        dto.WorkOrderAssigned = WoCount("Assigned");
        dto.WorkOrderInProgress = WoCount("InProgress");
        dto.WorkOrderOnHold = WoCount("OnHold");
        dto.WorkOrderCompleted = WoCount("Completed");
        dto.WorkOrderClosed = WoCount("Closed");
        dto.WorkOrderCancelled = WoCount("Cancelled");
        dto.WorkOrderRejected = WoCount("Rejected");
        dto.WorkOrderTotal = woCounts.Sum(x => x.Count);

        var now = DateTime.Now;
        dto.WorkOrderOverdue = await _context.WorkOrders.CountAsync(x =>
            x.IsActive && OpenWorkOrderStatuses.Contains(x.Status) && x.DueDate.HasValue && x.DueDate.Value < now);

        dto.WorkOrderCriticalOpen = await _context.WorkOrders.CountAsync(x =>
            x.IsActive && OpenWorkOrderStatuses.Contains(x.Status) && x.Priority == "Critical");

        dto.PmCompliance = await _intelligenceService.GetPmComplianceAsync(null, null);
        dto.MaintenanceTypeBreakdown = await _intelligenceService.GetMaintenanceTypeBreakdownAsync(null, null);

        return dto;
    }

    public async Task<PagedResult<MaintenanceBacklogItemDto>> GetBacklogAsync(
        string? status = null, string? priority = null, int? equipmentId = null,
        bool overdueOnly = false, int page = 1, int? pageSize = null)
    {
        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var normalizedPage = page < 1 ? 1 : page;
        var now = DateTime.Now;

        var query = _context.WorkOrders.Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);
        else
            query = query.Where(x => OpenWorkOrderStatuses.Contains(x.Status));

        if (!string.IsNullOrWhiteSpace(priority)) query = query.Where(x => x.Priority == priority);
        if (equipmentId.HasValue) query = query.Where(x => x.EquipmentId == equipmentId.Value);
        if (overdueOnly) query = query.Where(x => x.DueDate.HasValue && x.DueDate.Value < now);

        var totalCount = await query.CountAsync();

        // طبق ممیزی: ردیف‌های خام WorkOrder ابتدا صفحه‌بندی/Materialize می‌شوند، بعد Equipment/
        // Employee/Team با یک Lookup دسته‌ای جدا (نه Include/Select ترکیبی) پیوست می‌شوند - این
        // الگو از یک رفتار غیرمنتظره‌ی EF Core InMemory Provider (ترکیب Include یا Select با
        // Skip/Take و یک closure variable که باعث بازگشت خاموش صفر ردیف می‌شد، با اینکه
        // CountAsync همان کوئری را درست می‌شمرد) دوری می‌کند و در SQL Server هم صحیح است.
        var rawItems = await query
            .OrderBy(x => x.DueDate.HasValue ? 0 : 1)
            .ThenBy(x => x.DueDate)
            .ThenByDescending(x => x.Priority == "Critical")
            .ThenBy(x => x.CreatedDate)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

        var equipmentIds = rawItems.Select(x => x.EquipmentId).Distinct().ToList();
        var equipmentById = await _context.Equipments
            .Where(x => equipmentIds.Contains(x.EquipmentId))
            .ToDictionaryAsync(x => x.EquipmentId);

        var employeeIds = rawItems.Where(x => x.AssignedEmployeeId.HasValue).Select(x => x.AssignedEmployeeId!.Value).Distinct().ToList();
        var employeeById = await _context.Employees
            .Where(x => employeeIds.Contains(x.EmployeeId))
            .ToDictionaryAsync(x => x.EmployeeId);

        var teamIds = rawItems.Where(x => x.AssignedTeamId.HasValue).Select(x => x.AssignedTeamId!.Value).Distinct().ToList();
        var teamById = await _context.Teams
            .Where(x => teamIds.Contains(x.TeamId))
            .ToDictionaryAsync(x => x.TeamId);

        var items = rawItems.Select(x =>
        {
            equipmentById.TryGetValue(x.EquipmentId, out var equipment);
            var employee = x.AssignedEmployeeId.HasValue && employeeById.TryGetValue(x.AssignedEmployeeId.Value, out var emp) ? emp : null;
            var team = x.AssignedTeamId.HasValue && teamById.TryGetValue(x.AssignedTeamId.Value, out var tm) ? tm : null;

            return new MaintenanceBacklogItemDto
            {
                WorkOrderId = x.WorkOrderId,
                WorkOrderNumber = x.WorkOrderNumber,
                EquipmentTagNumber = equipment?.TagNumber,
                EquipmentDescription = equipment?.Description,
                WorkOrderType = x.WorkOrderType,
                Priority = x.Priority,
                Status = x.Status,
                DueDate = x.DueDate,
                CreatedDate = x.CreatedDate,
                IsOverdue = x.DueDate.HasValue && x.DueDate.Value < now,
                AgeDays = (int)(now - x.CreatedDate).TotalDays,
                AssignedEmployeeName = employee != null ? employee.FirstName + " " + employee.LastName : null,
                AssignedTeamName = team?.Name
            };
        }).ToList();

        return new PagedResult<MaintenanceBacklogItemDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }
}
