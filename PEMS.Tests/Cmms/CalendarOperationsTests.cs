using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Web.Services;

namespace PEMS.Tests.Cmms;

/// <summary>SCRUM-721: the My Work day list of the maintenance calendar as an operation.</summary>
public class CalendarOperationsTests
{
    private sealed class FakeCalendar : ICmmsCalendarService
    {
        public int? Employee; public DateTime? Day;
        public List<WorkOrderCalendarItemDto> Items = [];
        public Task<MaintenanceCalendarResultDto> GetCalendarAsync(DateTime rangeStart, DateTime rangeEnd, int? teamId, int? employeeId, string? status, string? workOrderType, string? priority, int? equipmentId, bool includeUnscheduled = true)
            => Task.FromResult(new MaintenanceCalendarResultDto());
        public Task<List<WorkOrderCalendarItemDto>> GetMyWorkAsync(int employeeId, DateTime day) { Employee = employeeId; Day = day; return Task.FromResult(Items); }
        public Task<List<CalendarFilterOptionDto>> GetActiveTeamsAsync() => Task.FromResult(new List<CalendarFilterOptionDto>());
    }

    [Fact]
    public async Task A_user_without_an_employee_sees_nothing_and_the_service_is_not_called()
    {
        var calendar = new FakeCalendar();

        var rows = await new CalendarOperations(calendar).MyWorkAsync(null);

        Assert.Empty(rows);
        Assert.Null(calendar.Employee);
    }

    [Fact]
    public async Task The_day_defaults_to_today_and_the_rows_are_ordered_by_start_with_the_conflict_marked()
    {
        var day = DateTime.Today;
        var calendar = new FakeCalendar
        {
            Items =
            [
                new WorkOrderCalendarItemDto { WorkOrderId = 2, WorkOrderNumber = "WO-2", PlannedStart = day.AddHours(13), PlannedFinish = day.AddHours(14), WorkOrderType = "Corrective", Status = "Open", HasEmployeeConflict = true },
                new WorkOrderCalendarItemDto { WorkOrderId = 1, WorkOrderNumber = "WO-1", PlannedStart = day.AddHours(8), PlannedFinish = day.AddHours(9), WorkOrderType = "Preventive", Status = "Open", EquipmentTagNumber = "P-101", AssignedTeamName = "Mech" }
            ]
        };

        var rows = await new CalendarOperations(calendar).MyWorkAsync(7);

        Assert.Equal(7, calendar.Employee);
        Assert.Equal(day, calendar.Day);
        Assert.Equal(["WO-1", "WO-2"], rows.Select(r => r.WorkOrderNumber));
        Assert.Equal("P-101", rows[0].Equipment);
        Assert.Equal("Mech", rows[0].Team);
        Assert.Equal("", rows[0].Conflict);
        Assert.Equal("!", rows[1].Conflict);
    }

    [Fact]
    public async Task A_given_date_is_used_without_its_time_part()
    {
        var calendar = new FakeCalendar();

        await new CalendarOperations(calendar).MyWorkAsync(7, new DateTime(2026, 11, 20, 15, 30, 0));

        Assert.Equal(new DateTime(2026, 11, 20), calendar.Day);
    }
}
