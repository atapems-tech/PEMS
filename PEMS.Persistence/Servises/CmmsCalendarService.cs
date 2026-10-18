using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS — Work Request/Work Order Scheduling &amp; Maintenance Calendar»: تقویم نگهداری یک
/// نمای خواندنی روی WorkOrder واقعی است (PlannedStart/PlannedFinish/AssignedTeamId/
/// AssignedEmployeeId) - دقیقاً هم‌الگو با CmmsOperationsService.GetBacklogAsync (ردیف‌های خام
/// اول Materialize می‌شوند، بعد Equipment/Employee/Team با Lookup دسته‌ای جدا پیوست می‌شوند تا از
/// رفتار غیرمنتظره‌ی EF Core InMemory Provider با Include/Select ترکیبی + closure دوری شود).
/// </summary>
public class CmmsCalendarService : ICmmsCalendarService
{
    private static readonly string[] OpenWorkOrderStatuses = { "Open", "Assigned", "InProgress", "OnHold" };

    private readonly PEMSDbContext _context;

    public CmmsCalendarService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<MaintenanceCalendarResultDto> GetCalendarAsync(
        DateTime rangeStart, DateTime rangeEnd, int? teamId, int? employeeId,
        string? status, string? workOrderType, string? priority, int? equipmentId,
        bool includeUnscheduled = true)
    {
        var now = DateTime.Now;

        // طبق بند ۳۱ درخواست: فیلتر بازه‌ی تاریخی در SQL انجام می‌شود، نه بارگذاری کل جدول.
        // هم‌پوشانی بازه: PlannedStart < rangeEnd && PlannedFinish > rangeStart.
        var query = _context.WorkOrders.Where(x =>
            x.IsActive && x.PlannedStart.HasValue && x.PlannedFinish.HasValue &&
            x.PlannedStart.Value < rangeEnd && x.PlannedFinish.Value > rangeStart);

        if (teamId.HasValue) query = query.Where(x => x.AssignedTeamId == teamId.Value);
        if (employeeId.HasValue) query = query.Where(x => x.AssignedEmployeeId == employeeId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(workOrderType)) query = query.Where(x => x.WorkOrderType == workOrderType);
        if (!string.IsNullOrWhiteSpace(priority)) query = query.Where(x => x.Priority == priority);
        if (equipmentId.HasValue) query = query.Where(x => x.EquipmentId == equipmentId.Value);

        var rawItems = await query.OrderBy(x => x.PlannedStart).ToListAsync();

        var items = await ProjectAsync(rawItems, now);

        var result = new MaintenanceCalendarResultDto { RangeStart = rangeStart, RangeEnd = rangeEnd, Items = items };

        if (includeUnscheduled)
        {
            // طبق بند ۱۹ درخواست: WorkOrder واجد شرایط برنامه‌ریزی (دیسیپلین مسئول تعیین‌شده، هنوز
            // در مرحله‌ی Assigned) که PlannedStart ندارد - مستقل از فیلتر بازه‌ی تاریخی.
            var unscheduledQuery = _context.WorkOrders.Where(x =>
                x.IsActive && x.Status == "Assigned" && x.PlannedStart == null && x.ResponsibleDisciplineId != null);
            if (teamId.HasValue) unscheduledQuery = unscheduledQuery.Where(x => x.AssignedTeamId == teamId.Value);
            if (employeeId.HasValue) unscheduledQuery = unscheduledQuery.Where(x => x.AssignedEmployeeId == employeeId.Value);
            if (!string.IsNullOrWhiteSpace(workOrderType)) unscheduledQuery = unscheduledQuery.Where(x => x.WorkOrderType == workOrderType);
            if (!string.IsNullOrWhiteSpace(priority)) unscheduledQuery = unscheduledQuery.Where(x => x.Priority == priority);
            if (equipmentId.HasValue) unscheduledQuery = unscheduledQuery.Where(x => x.EquipmentId == equipmentId.Value);

            var rawUnscheduled = await unscheduledQuery.OrderByDescending(x => x.Priority == "Critical").ThenBy(x => x.CreatedDate).Take(100).ToListAsync();
            result.Unscheduled = await ProjectUnscheduledAsync(rawUnscheduled);
        }

        return result;
    }

    public async Task<List<WorkOrderCalendarItemDto>> GetMyWorkAsync(int employeeId, DateTime day)
    {
        var dayStart = day.Date;
        var dayEnd = dayStart.AddDays(1);
        var now = DateTime.Now;

        var rawItems = await _context.WorkOrders.Where(x =>
                x.IsActive && x.AssignedEmployeeId == employeeId &&
                x.PlannedStart.HasValue && x.PlannedFinish.HasValue &&
                x.PlannedStart.Value < dayEnd && x.PlannedFinish.Value > dayStart)
            .OrderBy(x => x.PlannedStart)
            .ToListAsync();

        return await ProjectAsync(rawItems, now);
    }

    public async Task<List<CalendarFilterOptionDto>> GetActiveTeamsAsync() =>
        await _context.Teams.Where(x => x.IsActive)
            .Select(x => new CalendarFilterOptionDto { Id = x.TeamId, Name = x.Name })
            .OrderBy(x => x.Name)
            .ToListAsync();

    private async Task<List<WorkOrderCalendarItemDto>> ProjectAsync(List<WorkOrder> rawItems, DateTime now)
    {
        var equipmentIds = rawItems.Select(x => x.EquipmentId).Distinct().ToList();
        var equipmentById = await _context.Equipments.Where(x => equipmentIds.Contains(x.EquipmentId)).ToDictionaryAsync(x => x.EquipmentId);

        var employeeIds = rawItems.Where(x => x.AssignedEmployeeId.HasValue).Select(x => x.AssignedEmployeeId!.Value).Distinct().ToList();
        var employeeById = await _context.Employees.Where(x => employeeIds.Contains(x.EmployeeId)).ToDictionaryAsync(x => x.EmployeeId);

        var teamIds = rawItems.Where(x => x.AssignedTeamId.HasValue).Select(x => x.AssignedTeamId!.Value).Distinct().ToList();
        var teamById = await _context.Teams.Where(x => teamIds.Contains(x.TeamId)).ToDictionaryAsync(x => x.TeamId);

        // طبق بند ۱۸ درخواست: تداخل کارمند = دو WorkOrder فعال (نه Cancelled/Rejected) با همان
        // AssignedEmployeeId که بازه‌ی PlannedStart/PlannedFinیشان هم‌پوشانی دارد - فقط روی همین
        // مجموعه‌ی از قبل بارگذاری‌شده (کوچک، محدود به بازه‌ی درخواستی)، نه یک Query سراسری جدید.
        var conflictIds = new HashSet<int>();
        var byEmployee = rawItems.Where(x => x.AssignedEmployeeId.HasValue && x.Status != "Cancelled" && x.Status != "Rejected")
            .GroupBy(x => x.AssignedEmployeeId!.Value);
        foreach (var g in byEmployee)
        {
            var list = g.ToList();
            for (var i = 0; i < list.Count; i++)
                for (var j = i + 1; j < list.Count; j++)
                {
                    if (list[i].PlannedStart!.Value < list[j].PlannedFinish!.Value && list[j].PlannedStart!.Value < list[i].PlannedFinish!.Value)
                    {
                        conflictIds.Add(list[i].WorkOrderId);
                        conflictIds.Add(list[j].WorkOrderId);
                    }
                }
        }

        return rawItems.Select(x =>
        {
            equipmentById.TryGetValue(x.EquipmentId, out var equipment);
            var employee = x.AssignedEmployeeId.HasValue && employeeById.TryGetValue(x.AssignedEmployeeId.Value, out var emp) ? emp : null;
            var team = x.AssignedTeamId.HasValue && teamById.TryGetValue(x.AssignedTeamId.Value, out var tm) ? tm : null;

            return new WorkOrderCalendarItemDto
            {
                WorkOrderId = x.WorkOrderId,
                WorkOrderNumber = x.WorkOrderNumber,
                EquipmentTagNumber = equipment?.TagNumber,
                EquipmentDescription = equipment?.Description,
                WorkOrderType = x.WorkOrderType,
                Priority = x.Priority,
                Status = x.Status,
                PlannedStart = x.PlannedStart!.Value,
                PlannedFinish = x.PlannedFinish!.Value,
                AssignedTeamId = x.AssignedTeamId,
                AssignedTeamName = team?.Name,
                AssignedEmployeeId = x.AssignedEmployeeId,
                AssignedEmployeeName = employee != null ? employee.FirstName + " " + employee.LastName : null,
                IsOverdue = OpenWorkOrderStatuses.Contains(x.Status) && x.PlannedFinish.Value < now,
                HasEmployeeConflict = conflictIds.Contains(x.WorkOrderId)
            };
        }).ToList();
    }

    private async Task<List<UnscheduledWorkOrderDto>> ProjectUnscheduledAsync(List<WorkOrder> rawItems)
    {
        var equipmentIds = rawItems.Select(x => x.EquipmentId).Distinct().ToList();
        var equipmentById = await _context.Equipments.Where(x => equipmentIds.Contains(x.EquipmentId)).ToDictionaryAsync(x => x.EquipmentId);

        var employeeIds = rawItems.Where(x => x.AssignedEmployeeId.HasValue).Select(x => x.AssignedEmployeeId!.Value).Distinct().ToList();
        var employeeById = await _context.Employees.Where(x => employeeIds.Contains(x.EmployeeId)).ToDictionaryAsync(x => x.EmployeeId);

        var teamIds = rawItems.Where(x => x.AssignedTeamId.HasValue).Select(x => x.AssignedTeamId!.Value).Distinct().ToList();
        var teamById = await _context.Teams.Where(x => teamIds.Contains(x.TeamId)).ToDictionaryAsync(x => x.TeamId);

        return rawItems.Select(x =>
        {
            equipmentById.TryGetValue(x.EquipmentId, out var equipment);
            var employee = x.AssignedEmployeeId.HasValue && employeeById.TryGetValue(x.AssignedEmployeeId.Value, out var emp) ? emp : null;
            var team = x.AssignedTeamId.HasValue && teamById.TryGetValue(x.AssignedTeamId.Value, out var tm) ? tm : null;

            return new UnscheduledWorkOrderDto
            {
                WorkOrderId = x.WorkOrderId,
                WorkOrderNumber = x.WorkOrderNumber,
                EquipmentTagNumber = equipment?.TagNumber,
                EquipmentDescription = equipment?.Description,
                WorkOrderType = x.WorkOrderType,
                Priority = x.Priority,
                Status = x.Status,
                AssignedTeamName = team?.Name,
                AssignedEmployeeName = employee != null ? employee.FirstName + " " + employee.LastName : null
            };
        }).ToList();
    }
}
