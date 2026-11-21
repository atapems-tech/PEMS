namespace PEMS.Application.Interfaces;

/// <summary>
/// The write commands of InstrumentController as operations (SCRUM-695, 752): edit the instrument profile, record a calibration (append-only, it caches the last and next due on the instrument), add an alarm, a cause and effect,
/// assign the control loop and switch the instrument active or inactive. InstrumentController checked no module permission, so the operations carry none (signed-in users only, as before). The create, the work request and
/// the calibration work order, which redirect to the record they make, stay in the controller.
/// </summary>
public interface IInstrumentOperations
{
    [PEMS.Application.Common.PemsOperation("Instrument", "Edit")]
    Task<(bool Success, string? Error)> EditAsync(int instrumentId, string instrumentType, string? loopNumber = null, string? system = null, string? signalClass = null, decimal? rangeMin = null, decimal? rangeMax = null, string? unit = null, string? signalType = null, string? processVariable = null, string? accuracy = null, string? outputSignal = null, string? powerSupply = null, string? communication = null, string? ipRating = null, string? processConnection = null, string? electricalConnection = null, string? hazardousArea = null, string? exProtection = null, string? zone = null, string? gasGroup = null, string? tempClass = null, string? failAction = null, decimal? alarmSetpoint = null, decimal? tripSetpoint = null, string? sil = null, string? junctionBox = null, string? marshallingCabinet = null, string? cableNumber = null, string? barrierRelay = null, string? ioCardChannel = null, string? systemCabinet = null, int? calibrationInterval = null, string? currentStatus = null);

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
}
