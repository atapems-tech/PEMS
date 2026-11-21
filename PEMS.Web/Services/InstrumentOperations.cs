using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-695, 752: the write commands of InstrumentController, the logic moved without change (see <see cref="IInstrumentOperations"/>).</summary>
public sealed class InstrumentOperations(PEMSDbContext context) : IInstrumentOperations
{
    public async Task<(bool Success, string? Error)> EditAsync(int instrumentId, string instrumentType, string? loopNumber = null, string? system = null, string? signalClass = null, decimal? rangeMin = null, decimal? rangeMax = null, string? unit = null, string? signalType = null, string? processVariable = null, string? accuracy = null, string? outputSignal = null, string? powerSupply = null, string? communication = null, string? ipRating = null, string? processConnection = null, string? electricalConnection = null, string? hazardousArea = null, string? exProtection = null, string? zone = null, string? gasGroup = null, string? tempClass = null, string? failAction = null, decimal? alarmSetpoint = null, decimal? tripSetpoint = null, string? sil = null, string? junctionBox = null, string? marshallingCabinet = null, string? cableNumber = null, string? barrierRelay = null, string? ioCardChannel = null, string? systemCabinet = null, int? calibrationInterval = null, string? currentStatus = null)
    {
        var instrument = await context.Instruments.FirstOrDefaultAsync(x => x.InstrumentId == instrumentId);
        if (instrument == null) return (false, "Instrument not found.");

        instrument.InstrumentType = instrumentType;
        instrument.InstrumentType = instrumentType;
        instrument.LoopNumber = loopNumber;
        instrument.System = system;
        instrument.SignalClass = signalClass;
        instrument.RangeMin = rangeMin;
        instrument.RangeMax = rangeMax;
        instrument.Unit = unit;
        instrument.SignalType = signalType;
        instrument.ProcessVariable = processVariable;
        instrument.Accuracy = accuracy;
        instrument.OutputSignal = outputSignal;
        instrument.PowerSupply = powerSupply;
        instrument.Communication = communication;
        instrument.IPRating = ipRating;
        instrument.ProcessConnection = processConnection;
        instrument.ElectricalConnection = electricalConnection;
        instrument.HazardousArea = hazardousArea;
        instrument.ExProtection = exProtection;
        instrument.Zone = zone;
        instrument.GasGroup = gasGroup;
        instrument.TempClass = tempClass;
        instrument.FailAction = failAction;
        instrument.AlarmSetpoint = alarmSetpoint;
        instrument.TripSetpoint = tripSetpoint;
        instrument.SIL = sil;
        instrument.JunctionBox = junctionBox;
        instrument.MarshallingCabinet = marshallingCabinet;
        instrument.CableNumber = cableNumber;
        instrument.BarrierRelay = barrierRelay;
        instrument.IOCardChannel = ioCardChannel;
        instrument.SystemCabinet = systemCabinet;
        instrument.CalibrationInterval = calibrationInterval;
        instrument.CurrentStatus = string.IsNullOrWhiteSpace(currentStatus) ? instrument.CurrentStatus : currentStatus;
        await context.SaveChangesAsync();
        return (true, null);
    }

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
}
