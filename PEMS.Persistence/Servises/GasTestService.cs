using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق CMMS-10d: GasTest اتمی است - یک submit، همه‌ی GasReading ها با هم ثبت و ارزیابی
/// می‌شوند، Status بلافاصله Passed/Failed می‌شود (بدون حالت Draft). هیچ اکشن Edit/Update
/// وجود ندارد - اصلاح فقط از طریق InvalidateAsync (رکورد جدید + Invalidate قدیمی)، طبق نیاز
/// Immutability رکورد ایمنی‌محور در پلن.
/// </summary>
public class GasTestService : IGasTestService
{
    private readonly PEMSDbContext _context;

    public static readonly string[] ValidParameters = { "O2", "LEL", "H2S", "CO", "Other" };
    public static readonly string[] RequiredParameters = { "O2", "LEL", "H2S", "CO" };

    private static readonly Dictionary<string, string[]> LegalTransitions = new()
    {
        ["Passed"] = new[] { "Invalidated" },
        ["Failed"] = new[] { "Invalidated" },
    };

    public GasTestService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<GasTestListDto>> GetForPermitAsync(int permitId)
    {
        var tests = await _context.GasTests
            .Include(x => x.TesterEmployee)
            .Include(x => x.Readings)
            .Where(x => x.PermitId == permitId && x.IsActive)
            .OrderByDescending(x => x.TestedAt)
            .ToListAsync();

        var testIds = tests.Select(x => x.GasTestId).ToList();

        var histories = await _context.GasTestStatusHistories
            .Include(x => x.ChangedBy)
            .Where(x => testIds.Contains(x.GasTestId))
            .OrderByDescending(x => x.ChangedDate)
            .ToListAsync();

        var now = DateTime.Now;

        return tests.Select(t => new GasTestListDto
        {
            GasTestId = t.GasTestId,
            PermitId = t.PermitId,
            Location = t.Location,
            TestedAt = t.TestedAt,
            TesterName = t.TesterEmployee != null ? $"{t.TesterEmployee.FirstName} {t.TesterEmployee.LastName}" : null,
            InstrumentTag = t.InstrumentTag,
            InstrumentCalibrationDueDate = t.InstrumentCalibrationDueDate,
            InstrumentCalibrationStatus = ComputeInstrumentCalibrationStatus(t.InstrumentCalibrationDueDate, now),
            Status = t.Status,
            ValidUntil = t.ValidUntil,
            // طبق پلن: Expired یک وضعیت ذخیره‌شده نیست - Compute-on-read، دقیقاً همون الگوی
            // CalibrationStatus/ConditionStatus - هیچ‌وقت Status تاریخی را بازنویسی نمی‌کند.
            IsExpired = (t.Status == "Passed" || t.Status == "Failed") && t.ValidUntil.HasValue && t.ValidUntil.Value < now,
            InvalidationReason = t.InvalidationReason,
            Readings = t.Readings.Where(r => r.IsActive).Select(r => new GasReadingDto
            {
                Parameter = r.Parameter,
                Value = r.Value,
                Unit = r.Unit,
                Result = r.Result
            }).ToList(),
            StatusHistory = histories.Where(h => h.GasTestId == t.GasTestId)
                .Select(h => new GasTestStatusHistoryDto
                {
                    FromStatus = h.FromStatus,
                    ToStatus = h.ToStatus,
                    ChangedByName = h.ChangedBy != null ? $"{h.ChangedBy.FirstName} {h.ChangedBy.LastName}" : null,
                    ChangedDate = h.ChangedDate,
                    Reason = h.Reason
                }).ToList()
        }).ToList();
    }

    public async Task<(bool Success, string? Error, int? Id)> CreateAsync(
        int permitId, string location, DateTime testedAt, int testerEmployeeId,
        string? instrumentTag, DateTime? instrumentCalibrationDueDate,
        List<GasReadingInputDto> readings, int? equipmentId, int? functionalLocationId)
    {
        var permit = await _context.Permits.FirstOrDefaultAsync(x => x.PermitId == permitId && x.IsActive);
        if (permit == null) return (false, "Permit پیدا نشد.", null);

        location = location?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(location))
            return (false, "محل انجام تست الزامی است.", null);

        if (readings == null || readings.Count == 0)
            return (false, "حداقل یک قرائت گاز الزامی است.", null);

        foreach (var r in readings)
        {
            if (!ValidParameters.Contains(r.Parameter, StringComparer.Ordinal))
                return (false, $"پارامتر «{r.Parameter}» نامعتبره.", null);
            if (string.IsNullOrWhiteSpace(r.Unit))
                return (false, $"واحد اندازه‌گیری برای «{r.Parameter}» الزامی است.", null);
        }

        var missingRequired = RequiredParameters.Where(p => !readings.Any(r => r.Parameter == p)).ToList();
        if (missingRequired.Count > 0)
            return (false, $"پارامترهای الزامی ثبت نشده‌اند: {string.Join(", ", missingRequired)}.", null);

        // طبق تصمیم باز شماره ۱ پلن: بدون قاعده‌ی مدت اعتبار تاییدشده، Create مسدود می‌شود -
        // هیچ عددی حدس زده نمی‌شود.
        var validityRule = await _context.GasTestValidityRules
            .Where(x => x.IsActive && (x.PermitType == permit.PermitType || x.PermitType == null))
            .OrderBy(x => x.PermitType == null ? 1 : 0) // اولویت با قاعده‌ی اختصاصی نوع Permit
            .FirstOrDefaultAsync();

        if (validityRule == null)
            return (false, "دوره‌ی اعتبار تست گاز برای این نوع Permit پیکربندی نشده - با مدیر سیستم تماس بگیرید.", null);

        var limits = await _context.GasParameterLimits
            .Where(x => x.IsActive && (x.PermitType == permit.PermitType || x.PermitType == null))
            .ToListAsync();

        var gasTest = new GasTest
        {
            PermitId = permitId,
            EquipmentId = equipmentId,
            FunctionalLocationId = functionalLocationId,
            Location = location,
            TestedAt = testedAt,
            TesterEmployeeId = testerEmployeeId,
            InstrumentTag = instrumentTag,
            InstrumentCalibrationDueDate = instrumentCalibrationDueDate,
            ValidUntil = testedAt.AddMinutes(validityRule.ValidityMinutes),
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        var readingEntities = new List<GasReading>();
        var anyFail = false;

        foreach (var r in readings)
        {
            var limit = limits.Where(l => l.Parameter == r.Parameter)
                .OrderBy(l => l.PermitType == null ? 1 : 0)
                .FirstOrDefault();

            string result;
            if (limit == null)
            {
                // طبق پلن: بدون حد پذیرش پیکربندی‌شده، ارزیابی ممکن نیست - Fail-safe به‌جای
                // Pass پیش‌فرض (همون منطق §22 سند: عدم قطعیت = رد، نه پذیرش).
                result = "Unknown";
                anyFail = true;
            }
            else
            {
                var withinMin = !limit.MinValue.HasValue || r.Value >= limit.MinValue.Value;
                var withinMax = !limit.MaxValue.HasValue || r.Value <= limit.MaxValue.Value;
                result = withinMin && withinMax ? "Pass" : "Fail";
                if (result == "Fail") anyFail = true;
            }

            readingEntities.Add(new GasReading
            {
                Parameter = r.Parameter,
                Value = r.Value,
                Unit = r.Unit,
                Result = result,
                IsActive = true,
                CreatedDate = DateTime.Now
            });
        }

        gasTest.Status = anyFail ? "Failed" : "Passed";
        gasTest.Readings = readingEntities;

        await _context.GasTests.AddAsync(gasTest);
        await _context.SaveChangesAsync();

        await _context.GasTestStatusHistories.AddAsync(new GasTestStatusHistory
        {
            GasTestId = gasTest.GasTestId,
            FromStatus = "",
            ToStatus = gasTest.Status,
            ChangedById = testerEmployeeId,
            ChangedDate = DateTime.Now,
            Reason = "ثبت اولیه‌ی تست گاز"
        });
        await _context.SaveChangesAsync();

        return (true, null, gasTest.GasTestId);
    }

    public async Task<(bool Success, string? Error)> InvalidateAsync(
        int gasTestId, int permitId, string reason, int? actorEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return (false, "دلیل ابطال الزامی است.");

        var gasTest = await _context.GasTests.FirstOrDefaultAsync(x => x.GasTestId == gasTestId);
        if (gasTest == null) return (false, "تست گاز پیدا نشد.");

        // طبق CMMS-10d بند ۱۲ دستورالعمل: اعتبارسنجی مالکیت سمت سرور - IsolationController
        // این بررسی را ندارد (بند ۶ پلن)، این‌جا عمداً اجرا می‌شود.
        if (gasTest.PermitId != permitId)
            return (false, "این تست گاز متعلق به این Permit نیست.");

        var fromStatus = gasTest.Status;
        if (!LegalTransitions.TryGetValue(fromStatus, out var allowed) || !allowed.Contains("Invalidated"))
            return (false, $"گذار از وضعیت «{fromStatus}» به «Invalidated» مجاز نیست.");

        gasTest.Status = "Invalidated";
        gasTest.InvalidationReason = reason;

        await _context.GasTestStatusHistories.AddAsync(new GasTestStatusHistory
        {
            GasTestId = gasTest.GasTestId,
            FromStatus = fromStatus,
            ToStatus = "Invalidated",
            ChangedById = actorEmployeeId,
            ChangedDate = DateTime.Now,
            Reason = reason
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    private static string ComputeInstrumentCalibrationStatus(DateTime? dueDate, DateTime now)
    {
        if (!dueDate.HasValue) return "Unknown";
        if (dueDate.Value < now) return "Overdue";
        if (dueDate.Value < now.AddDays(30)) return "DueSoon";
        return "OK";
    }
}
