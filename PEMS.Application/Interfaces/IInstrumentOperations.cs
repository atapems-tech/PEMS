namespace PEMS.Application.Interfaces;

public sealed record InstrumentListRow(int Id, string EquipmentTag, string Manufacturer, string Model, string InstrumentType, string LoopNumber, string System, DateTime? LastCalibrationDate, DateTime? NextCalibrationDue, string CalibrationStatus);

public sealed record InstrumentCountsRow(int Total, int Overdue, int DueSoon, int Ok, int Unknown);

public sealed record InstrumentHeadRow(int Id, string EquipmentTag, string InstrumentType, string LoopNumber, string System, string SignalClass, string ProcessVariable, string Range, string Unit, string SignalType, string Accuracy,
    string CurrentStatus, string CalibrationStatus, int? CalibrationInterval, DateTime? LastCalibrationDate, DateTime? NextCalibrationDue, bool IsActive, DateTime? LatestMaintenanceDate, string LatestMaintenance);

public sealed record InstrumentCalibrationRow(int Id, DateTime CalibrationDate, string CalibrationType, string StandardReference, string AsFound, string AsLeft, string Result, string Technician, string CertificateNo, DateTime? NextDue, string Remarks);

public sealed record InstrumentAlarmRow(int Id, string AlarmTag, string AlarmType, string Priority, string SetPoint, string Unit, string Description, string Action);

public sealed record InstrumentCauseEffectRow(int Id, string AlarmTag, string Cause, string Effect, string ActionType, string TripSignal);

public sealed record InstrumentControlLoopRow(int Id, string LoopNumber, string Description, string ProcessArea, string ControllerType, string ControllerSystem);

public sealed record InstrumentIoPointRow(int Id, string TagNumber, string IoType, string SignalType, string PlcName, string Rack, string Slot, string Channel, string Address, string Description);

/// <summary>
/// The instrument passport as operations (SCRUM-708): the list, the passport head and its calibrations, alarms, cause and effect, control loop and IO points (reads); the calibration, alarm, cause and effect and control loop commands
/// (append-only calibrations cache the due dates on the instrument); the active switch; and the two commands that make another record from the passport: a work request from a cause and effect and a calibration work order. The
/// controller checked no module permission, so the operations carry none (signed-in users only, as before). Create and edit are entity definitions.
/// </summary>
public interface IInstrumentOperations
{
    [PEMS.Application.Common.PemsOperation("Instrument", "Counts")]
    Task<List<InstrumentCountsRow>> CountsAsync();

    [PEMS.Application.Common.PemsOperation("Instrument", "List")]
    Task<List<InstrumentListRow>> ListAsync();

    [PEMS.Application.Common.PemsOperation("Instrument", "Head")]
    Task<List<InstrumentHeadRow>> HeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("Instrument", "Calibrations")]
    Task<List<InstrumentCalibrationRow>> CalibrationsAsync(int id);

    [PEMS.Application.Common.PemsOperation("Instrument", "Alarms")]
    Task<List<InstrumentAlarmRow>> AlarmsAsync(int id);

    [PEMS.Application.Common.PemsOperation("Instrument", "CauseEffects")]
    Task<List<InstrumentCauseEffectRow>> CauseEffectsAsync(int id);

    [PEMS.Application.Common.PemsOperation("Instrument", "ControlLoop")]
    Task<List<InstrumentControlLoopRow>> ControlLoopAsync(int id);

    [PEMS.Application.Common.PemsOperation("Instrument", "IoPoints")]
    Task<List<InstrumentIoPointRow>> IoPointsAsync(int id);

    [PEMS.Application.Common.PemsOperation("Instrument", "AddCalibration")]
    Task<(bool Success, string? Error)> AddCalibrationAsync(int instrumentId, DateTime calibrationDate, string result, string? calibrationType = null, string? standardReference = null, string? asFound = null,
        string? asLeft = null, int? technicianEmployeeId = null, string? certificateNo = null, string? remarks = null);

    [PEMS.Application.Common.PemsOperation("Instrument", "AddAlarm")]
    Task<(bool Success, string? Error)> AddAlarmAsync(int instrumentId, string alarmTag, string alarmType, string priority, decimal? setPoint = null, string? unit = null, string? description = null, string? action = null);

    [PEMS.Application.Common.PemsOperation("Instrument", "AddCauseEffect")]
    Task<(bool Success, string? Error)> AddCauseEffectAsync(int alarmId, string cause, string effect, string? actionType = null, string? tripSignal = null, string? description = null);

    [PEMS.Application.Common.PemsOperation("Instrument", "AssignControlLoop")]
    Task<(bool Success, string? Error)> AssignControlLoopAsync(int instrumentId, string loopNumber, string? description = null, string? processArea = null, string? controllerType = null, string? controllerSystem = null);

    [PEMS.Application.Common.PemsOperation("Instrument", "ToggleActive")]
    Task<(bool Success, string? Error)> ToggleActiveAsync(int id);

    [PEMS.Application.Common.PemsOperation("Instrument", "RaiseWorkRequest")]
    Task<(bool Success, string? Error)> RaiseWorkRequestAsync(int causeEffectId);

    [PEMS.Application.Common.PemsOperation("Instrument", "GenerateCalibrationWorkOrder")]
    Task<(bool Success, string? Error)> GenerateCalibrationWorkOrderAsync(int instrumentId);
}
