using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق CMMS Production Baseline v1 - CMMS-07 (Inspection): بازرسی دوره‌ای کمی تجهیز
/// (Condition Monitoring) - InspectionRecord را با ITPLineItemId=Null ثبت می‌کند (نقطه‌ی ورود
/// مستقل از پذیرش ITP که QCController.ItpPerformInspection مدیریت می‌کند).
/// </summary>
public class InspectionService : IInspectionService
{
    private readonly PEMSDbContext _context;

    public InspectionService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<InspectionListItemDto>> GetFleetAsync()
    {
        var records = await _context.InspectionRecords
            .Where(x => x.IsActive)
            .Include(x => x.Equipment)
            .OrderByDescending(x => x.InspectionDate)
            .ToListAsync();

        return SortByPriority(records.Select(ToDto));
    }

    public async Task<PagedResult<InspectionListItemDto>> GetPagedFleetAsync(int page, int pageSize)
    {
        var all = await GetFleetAsync();

        page = page < 1 ? 1 : page;
        pageSize = DataGridPageSize.Normalize(pageSize);

        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<InspectionListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = all.Count };
    }

    private static List<InspectionListItemDto> SortByPriority(IEnumerable<InspectionListItemDto> items) => items
        .OrderBy(x => x.ConditionStatus == "Alert" ? 0 : x.DueStatus == "Overdue" ? 1 : x.DueStatus == "DueSoon" ? 2 : 3)
        .ThenByDescending(x => x.InspectionDate)
        .ThenByDescending(x => x.InspectionRecordId)
        .ToList();

    public async Task<List<InspectionListItemDto>> GetForEquipmentAsync(int equipmentId)
    {
        var records = await _context.InspectionRecords
            .Where(x => x.EquipmentId == equipmentId && x.IsActive)
            .Include(x => x.Equipment)
            .OrderByDescending(x => x.InspectionDate)
            .ToListAsync();

        return records.Select(ToDto).ToList();
    }

    public async Task<(bool Success, string? Error, int? InspectionRecordId)> CreateAsync(
        int equipmentId, string inspectionType, DateTime inspectionDate, string? inspector,
        decimal? measurementValue, string? measurementUnit, decimal? lowerLimit, decimal? upperLimit,
        string? recommendation, DateTime? nextInspectionDate, string? description)
    {
        if (string.IsNullOrWhiteSpace(inspectionType)) return (false, "نوع بازرسی لازم است.", null);

        var equipmentExists = await _context.Equipments.AnyAsync(x => x.EquipmentId == equipmentId && x.IsActive);
        if (!equipmentExists) return (false, "تجهیز انتخاب‌شده نامعتبره.", null);

        var conditionStatus = GetConditionStatus(measurementValue, lowerLimit, upperLimit);

        var record = new InspectionRecord
        {
            EquipmentId = equipmentId,
            InspectionType = inspectionType.Trim(),
            InspectionDate = inspectionDate,
            Inspector = inspector,
            MeasurementValue = measurementValue,
            MeasurementUnit = measurementUnit,
            LowerLimit = lowerLimit,
            UpperLimit = upperLimit,
            Result = conditionStatus == "Alert" ? "Warning" : "Acceptable",
            Recommendation = recommendation,
            NextInspectionDate = nextInspectionDate,
            Description = description,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.InspectionRecords.AddAsync(record);
        await _context.SaveChangesAsync();

        return (true, null, record.InspectionRecordId);
    }

    private static InspectionListItemDto ToDto(InspectionRecord x) => new()
    {
        InspectionRecordId = x.InspectionRecordId,
        EquipmentId = x.EquipmentId,
        TagNumber = x.Equipment?.TagNumber ?? string.Empty,
        InspectionType = x.InspectionType,
        InspectionDate = x.InspectionDate,
        Inspector = x.Inspector,
        MeasurementValue = x.MeasurementValue,
        MeasurementUnit = x.MeasurementUnit,
        LowerLimit = x.LowerLimit,
        UpperLimit = x.UpperLimit,
        ConditionStatus = GetConditionStatus(x.MeasurementValue, x.LowerLimit, x.UpperLimit),
        NextInspectionDate = x.NextInspectionDate,
        DueStatus = GetDueStatus(x.NextInspectionDate),
        Recommendation = x.Recommendation,
        Description = x.Description
    };

    // طبق زنجیره‌ی Measurement → Limit → Condition (بند ۲۵ سند Architecture Gate): مقایسه‌ی
    // ساده‌ی یک قرائت با آستانه - بدون تحلیل روند/آماری (پیش‌بینانه هنوز پیاده نمی‌شود).
    private static string GetConditionStatus(decimal? value, decimal? lower, decimal? upper)
    {
        if (!value.HasValue || (!lower.HasValue && !upper.HasValue)) return "Unknown";
        if (lower.HasValue && value.Value < lower.Value) return "Alert";
        if (upper.HasValue && value.Value > upper.Value) return "Alert";
        return "Normal";
    }

    // همان منطق EquipmentService.GetCalibrationStatus - برای هم‌خوانی وضعیت سررسید در کل پروژه
    private static string GetDueStatus(DateTime? nextDue)
    {
        if (!nextDue.HasValue) return "Unknown";
        if (nextDue.Value.Date < DateTime.Today) return "Overdue";
        if (nextDue.Value.Date <= DateTime.Today.AddDays(30)) return "DueSoon";
        return "OK";
    }
}
