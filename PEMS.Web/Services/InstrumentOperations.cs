using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-708: the instrument passport, the logic of InstrumentController moved without change (see <see cref="IInstrumentOperations"/>).</summary>
public sealed class InstrumentOperations(PEMSDbContext context, IInstrumentService instruments, IWorkRequestService workRequests, INumberingService numbering, ICurrentUserService currentUser,
    IWorkOrderLifecycleService? workOrderLifecycle = null) : IInstrumentOperations
{
    private const int RowLimit = 5000;

    // Overdue when the due date has passed, DueSoon within the next 30 days, Unknown when no due date is recorded (never guessed)
    private static string CalibrationStatus(DateTime? nextDue)
    {
        if (!nextDue.HasValue) return "Unknown";
        if (nextDue.Value.Date < DateTime.Today) return "Overdue";
        if (nextDue.Value.Date <= DateTime.Today.AddDays(30)) return "DueSoon";
        return "OK";
    }

    private async Task<int?> EmployeeIdAsync()
    {
        if (!currentUser.UserId.HasValue) return null;
        return await context.Users.Where(u => u.Id == currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }

    // ---- reads
    public async Task<List<InstrumentListRow>> ListAsync()
        => (await instruments.GetListAsync(1, RowLimit, null, null)).Items
            .Select(i => new InstrumentListRow(i.InstrumentId, i.TagNumber, i.Manufacturer ?? "", i.Model ?? "", i.InstrumentType, i.LoopNumber ?? "", i.System ?? "", i.LastCalibrationDate, i.NextCalibrationDue, i.CalibrationStatus)).ToList();

    public async Task<List<InstrumentHeadRow>> HeadAsync(int id)
    {
        var i = await context.Instruments.AsNoTracking().Include(x => x.Equipment).FirstOrDefaultAsync(x => x.InstrumentId == id);
        if (i == null) return [];
        var maintenance = await context.MaintenanceHistories.AsNoTracking().Where(x => x.EquipmentId == i.EquipmentId).OrderByDescending(x => x.ExecutionDate).FirstOrDefaultAsync();
        var range = i.RangeMin.HasValue || i.RangeMax.HasValue ? $"{i.RangeMin} - {i.RangeMax}" : "";
        return [new(i.InstrumentId, i.Equipment?.TagNumber ?? "", i.InstrumentType, i.LoopNumber ?? "", i.System ?? "", i.SignalClass ?? "", i.ProcessVariable ?? "", range, i.Unit ?? "", i.SignalType ?? "", i.Accuracy ?? "",
            i.CurrentStatus, CalibrationStatus(i.NextCalibrationDue), i.CalibrationInterval, i.LastCalibrationDate, i.NextCalibrationDue, i.IsActive, maintenance?.ExecutionDate, maintenance?.ActionTaken ?? maintenance?.Description ?? "")];
    }

    public async Task<List<InstrumentCalibrationRow>> CalibrationsAsync(int id)
        => await context.CalibrationHistories.AsNoTracking().Include(x => x.Technician).Where(x => x.InstrumentId == id).OrderByDescending(x => x.CalibrationDate)
            .Select(c => new InstrumentCalibrationRow(c.CalibrationHistoryId, c.CalibrationDate, c.CalibrationType, c.StandardReference ?? "", c.AsFound ?? "", c.AsLeft ?? "", c.Result,
                c.Technician != null ? c.Technician.FirstName + " " + c.Technician.LastName : "", c.CertificateNo ?? "", c.NextDue, c.Remarks ?? "")).ToListAsync();

    public async Task<List<InstrumentAlarmRow>> AlarmsAsync(int id)
        => (await context.Alarms.AsNoTracking().Where(x => x.InstrumentId == id && x.IsActive).OrderBy(x => x.AlarmType).ToListAsync())
            .Select(a => new InstrumentAlarmRow(a.AlarmId, a.AlarmTag, a.AlarmType, a.Priority, a.SetPoint?.ToString() ?? "", a.Unit ?? "", a.Description ?? "", a.Action ?? "")).ToList();

    public async Task<List<InstrumentCauseEffectRow>> CauseEffectsAsync(int id)
        => await context.CauseEffects.AsNoTracking().Include(x => x.Alarm).Where(x => x.Alarm!.InstrumentId == id && x.Alarm.IsActive && x.IsActive)
            .Select(c => new InstrumentCauseEffectRow(c.CauseEffectId, c.Alarm!.AlarmTag, c.Cause, c.Effect, c.ActionType ?? "", c.TripSignal ?? "")).ToListAsync();

    public async Task<List<InstrumentControlLoopRow>> ControlLoopAsync(int id)
        => await context.ControlLoops.AsNoTracking().Where(x => x.InstrumentId == id && x.IsActive)
            .Select(l => new InstrumentControlLoopRow(l.LoopId, l.LoopNumber, l.Description ?? "", l.ProcessArea ?? "", l.ControllerType ?? "", l.ControllerSystem ?? "")).ToListAsync();

    public async Task<List<InstrumentIoPointRow>> IoPointsAsync(int id)
        => await context.IOPoints.AsNoTracking().Where(x => x.InstrumentId == id && x.IsActive)
            .Select(p => new InstrumentIoPointRow(p.IOPointId, p.TagNumber, p.IOType, p.SignalType ?? "", p.PLCName ?? "", p.Rack ?? "", p.Slot ?? "", p.Channel ?? "", p.Address ?? "", p.Description ?? "")).ToListAsync();

    // ---- commands
    // append-only like CalibrationHistory: no update or delete of an earlier record; the due dates are cached on the instrument for the master list
    public async Task<(bool Success, string? Error)> AddCalibrationAsync(int instrumentId, DateTime calibrationDate, string result, string? calibrationType = null, string? standardReference = null, string? asFound = null,
        string? asLeft = null, int? technicianEmployeeId = null, string? certificateNo = null, string? remarks = null)
    {
        var instrument = await context.Instruments.FirstOrDefaultAsync(x => x.InstrumentId == instrumentId);
        if (instrument == null) return (false, "Instrument not found.");

        DateTime? nextDue = instrument.CalibrationInterval.HasValue ? calibrationDate.AddDays(instrument.CalibrationInterval.Value) : null;
        await context.CalibrationHistories.AddAsync(new CalibrationHistory
        {
            InstrumentId = instrumentId, CalibrationDate = calibrationDate, CalibrationType = string.IsNullOrWhiteSpace(calibrationType) ? "Routine" : calibrationType,
            StandardReference = standardReference, AsFound = asFound, AsLeft = asLeft, Result = string.IsNullOrWhiteSpace(result) ? "Pass" : result,
            TechnicianEmployeeId = technicianEmployeeId, CertificateNo = certificateNo, NextDue = nextDue, Remarks = remarks, CreatedDate = DateTime.Now
        });
        instrument.LastCalibrationDate = calibrationDate;
        instrument.NextCalibrationDue = nextDue;
        await context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AddAlarmAsync(int instrumentId, string alarmTag, string alarmType, string priority, decimal? setPoint = null, string? unit = null, string? description = null, string? action = null)
    {
        if (!await context.Instruments.AnyAsync(x => x.InstrumentId == instrumentId)) return (false, "Instrument not found.");
        await context.Alarms.AddAsync(new Alarm
        {
            InstrumentId = instrumentId, AlarmTag = alarmTag, AlarmType = alarmType, Priority = priority, SetPoint = setPoint, Unit = unit, Description = description, Action = action, IsActive = true, CreatedDate = DateTime.Now
        });
        await context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AddCauseEffectAsync(int alarmId, string cause, string effect, string? actionType = null, string? tripSignal = null, string? description = null)
    {
        if (!await context.Alarms.AnyAsync(x => x.AlarmId == alarmId)) return (false, "Alarm not found.");
        await context.CauseEffects.AddAsync(new CauseEffect
        {
            AlarmId = alarmId, Cause = cause, Effect = effect, ActionType = actionType, TripSignal = tripSignal, Description = description, IsActive = true, CreatedDate = DateTime.Now
        });
        await context.SaveChangesAsync();
        return (true, null);
    }

    // one main control loop per instrument, like the one-to-one instrument and equipment
    public async Task<(bool Success, string? Error)> AssignControlLoopAsync(int instrumentId, string loopNumber, string? description = null, string? processArea = null, string? controllerType = null, string? controllerSystem = null)
    {
        if (!await context.Instruments.AnyAsync(x => x.InstrumentId == instrumentId)) return (false, "Instrument not found.");
        if (await context.ControlLoops.AnyAsync(x => x.InstrumentId == instrumentId && x.IsActive)) return (false, "این Instrument از قبل به یک Control Loop وصل است.");
        await context.ControlLoops.AddAsync(new ControlLoop
        {
            LoopNumber = loopNumber, Description = description, ProcessArea = processArea, ControllerType = controllerType, ControllerSystem = controllerSystem, InstrumentId = instrumentId, IsActive = true, CreatedDate = DateTime.Now
        });
        await context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ToggleActiveAsync(int id)
    {
        var instrument = await context.Instruments.FirstOrDefaultAsync(x => x.InstrumentId == id);
        if (instrument == null) return (false, "Instrument not found.");
        instrument.IsActive = !instrument.IsActive;
        await context.SaveChangesAsync();
        return (true, null);
    }

    // one human click, like Alert to WorkRequest in the inspection module: there is no live event source (SCADA/DCS feed) to raise a work request from
    public async Task<(bool Success, string? Error)> RaiseWorkRequestAsync(int causeEffectId)
    {
        var causeEffect = await context.CauseEffects.Include(x => x.Alarm).ThenInclude(a => a!.Instrument).ThenInclude(i => i!.Equipment).FirstOrDefaultAsync(x => x.CauseEffectId == causeEffectId);
        if (causeEffect?.Alarm?.Instrument == null) return (false, "Cause and effect not found.");

        var employeeId = await EmployeeIdAsync();
        if (!employeeId.HasValue) return (false, "کارمند مرتبط با این کاربر پیدا نشد.");

        var instrument = causeEffect.Alarm.Instrument;
        var title = $"I&C Alarm/Trip - {causeEffect.Alarm.AlarmTag} - Equipment #{instrument.EquipmentId}";
        var reportedCondition = $"آلارم {causeEffect.Alarm.AlarmTag} ({causeEffect.Alarm.AlarmType}): علت «{causeEffect.Cause}» → نتیجه «{causeEffect.Effect}»" +
            (string.IsNullOrWhiteSpace(causeEffect.ActionType) ? "" : $" - اقدام: {causeEffect.ActionType}") +
            (string.IsNullOrWhiteSpace(causeEffect.TripSignal) ? "" : $" (سیگنال: {causeEffect.TripSignal})") + ".";
        var priority = causeEffect.Alarm.Priority is "Critical" or "High" ? causeEffect.Alarm.Priority : "High";

        var (success, error, _) = await workRequests.CreateAsync(title, reportedCondition, employeeId.Value, instrument.EquipmentId, null, null, reportedCondition, priority, null);
        return success ? (true, null) : (false, error);
    }

    public async Task<(bool Success, string? Error)> GenerateCalibrationWorkOrderAsync(int instrumentId)
    {
        var instrument = await context.Instruments.Include(x => x.Equipment).FirstOrDefaultAsync(x => x.InstrumentId == instrumentId);
        if (instrument == null) return (false, "Instrument not found.");

        var workOrderNumber = await numbering.GetNextNumberAsync("WorkOrder");
        await context.WorkOrders.AddAsync(new WorkOrder
        {
            EquipmentId = instrument.EquipmentId, WorkOrderNumber = workOrderNumber, WorkOrderType = "Preventive", Priority = "Medium", Status = "Open",
            Description = $"Calibration due for instrument {instrument.Equipment?.TagNumber}", IsActive = true, CreatedDate = DateTime.Now
        });
        await context.SaveChangesAsync();

        // Phase 3B: the generated work order becomes governed (origin snapshot of the user and the start of its lifecycle); without a binding it is inert
        if (workOrderLifecycle != null)
        {
            var (ok, govError, _) = await workOrderLifecycle.GovernPendingNewAsync(new PEMS.Application.DTOs.WorkOrderActor(await EmployeeIdAsync(), currentUser.Roles));
            if (!ok) return (false, govError);
        }
        return (true, null);
    }
}
