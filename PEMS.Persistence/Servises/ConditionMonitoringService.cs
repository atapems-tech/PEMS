using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۵. VariableType به یک فهرست ثابت محدود شده - از مقدار دلخواه
/// جلوگیری می‌کند بدون این‌که یک جدول Lookup جدید اضافه کند (سازگار با بند ۲۵: فقط وقتی
/// واقعاً لازم است Migration اضافه کن).
/// </summary>
public class ConditionMonitoringService : IConditionMonitoringService
{
    public static readonly string[] ValidVariableTypes =
    {
        "Vibration", "Temperature", "Pressure", "Flow", "Current", "Voltage",
        "Lubrication", "OilAnalysis", "Thermography", "Acoustic", "ProcessDeviation"
    };

    private readonly PEMSDbContext _context;

    public ConditionMonitoringService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<ConditionMonitoringFoundationStatusDto> GetFoundationStatusAsync()
    {
        var pointCount = await _context.ConditionMonitoringPoints.CountAsync(x => x.IsActive);
        var readingCount = await _context.ConditionReadings.CountAsync();

        return new ConditionMonitoringFoundationStatusDto
        {
            FoundationStatus = "READY",
            LiveTelemetryStatus = readingCount > 0 ? "PARTIAL" : "NOT_READY",
            MonitoringPointCount = pointCount,
            ReadingCount = readingCount,
            SupportedVariableTypes = ValidVariableTypes.ToList()
        };
    }

    public async Task<List<ConditionMonitoringPointDto>> GetPointsForEquipmentAsync(int equipmentId)
    {
        var points = await _context.ConditionMonitoringPoints
            .Where(x => x.EquipmentId == equipmentId && x.IsActive)
            .Include(x => x.Equipment)
            .ToListAsync();

        var pointIds = points.Select(x => x.ConditionMonitoringPointId).ToList();
        var lastReadings = await _context.ConditionReadings
            .Where(x => pointIds.Contains(x.ConditionMonitoringPointId))
            .GroupBy(x => x.ConditionMonitoringPointId)
            .Select(g => new
            {
                PointId = g.Key,
                Count = g.Count(),
                Last = g.OrderByDescending(x => x.ReadingDate).First()
            })
            .ToListAsync();

        return points.Select(p =>
        {
            var last = lastReadings.FirstOrDefault(x => x.PointId == p.ConditionMonitoringPointId);
            return new ConditionMonitoringPointDto
            {
                ConditionMonitoringPointId = p.ConditionMonitoringPointId,
                EquipmentId = p.EquipmentId,
                EquipmentTagNumber = p.Equipment?.TagNumber,
                VariableType = p.VariableType,
                Tag = p.Tag,
                Unit = p.Unit,
                AlarmThresholdLow = p.AlarmThresholdLow,
                AlarmThresholdHigh = p.AlarmThresholdHigh,
                ReadingCount = last?.Count ?? 0,
                LastReadingDate = last?.Last.ReadingDate,
                LastValue = last?.Last.Value,
                LastReadingIsAlarm = last?.Last.IsAlarm ?? false
            };
        }).ToList();
    }

    public async Task<(bool Success, string? Error, int? PointId)> CreatePointAsync(
        int equipmentId, string variableType, string tag, string? unit,
        decimal? alarmLow, decimal? alarmHigh, string? description, string? createdBy)
    {
        if (!ValidVariableTypes.Contains(variableType))
            return (false, $"نوع متغیر نامعتبر است. مقادیر مجاز: {string.Join(", ", ValidVariableTypes)}", null);

        if (string.IsNullOrWhiteSpace(tag))
            return (false, "Tag نقطه‌ی پایش الزامی است.", null);

        var equipmentExists = await _context.Equipments.AnyAsync(x => x.EquipmentId == equipmentId && x.IsActive);
        if (!equipmentExists) return (false, "تجهیز یافت نشد.", null);

        var point = new ConditionMonitoringPoint
        {
            EquipmentId = equipmentId,
            VariableType = variableType,
            Tag = tag,
            Unit = unit,
            AlarmThresholdLow = alarmLow,
            AlarmThresholdHigh = alarmHigh,
            Description = description,
            IsActive = true,
            CreatedDate = DateTime.Now,
            CreatedBy = createdBy
        };

        await _context.ConditionMonitoringPoints.AddAsync(point);
        await _context.SaveChangesAsync();

        return (true, null, point.ConditionMonitoringPointId);
    }

    public async Task<(bool Success, string? Error)> RecordReadingAsync(
        int pointId, DateTime readingDate, decimal value, string source, string? notes, string? recordedBy)
    {
        var point = await _context.ConditionMonitoringPoints.FirstOrDefaultAsync(x => x.ConditionMonitoringPointId == pointId && x.IsActive);
        if (point == null) return (false, "نقطه‌ی پایش یافت نشد.");

        var isAlarm = (point.AlarmThresholdLow.HasValue && value < point.AlarmThresholdLow.Value) ||
                      (point.AlarmThresholdHigh.HasValue && value > point.AlarmThresholdHigh.Value);

        var reading = new ConditionReading
        {
            ConditionMonitoringPointId = pointId,
            ReadingDate = readingDate,
            Value = value,
            Source = string.IsNullOrWhiteSpace(source) ? "Manual" : source,
            Provenance = "OBSERVED",
            IsAlarm = isAlarm,
            Notes = notes,
            RecordedBy = recordedBy,
            CreatedDate = DateTime.Now
        };

        await _context.ConditionReadings.AddAsync(reading);
        await _context.SaveChangesAsync();

        return (true, null);
    }
}
