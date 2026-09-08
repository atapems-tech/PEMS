using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS Phase 2 — ISO 14224 Failure Taxonomy» بند ۱۳/۳۱: خط‌مبنای Failure Mode به‌ازای
/// خانواده‌ی تجهیز (نه رکورد به رکورد). این‌جا Data-Driven است، نه یک تناظر سفت‌وسخت ۱ به ۱ - هر
/// Mode می‌تواند به چند Cause متصل باشد (از طریق FailureModeCause، طبق تصمیم معماری بند ۱۲ که در
/// FailureTaxonomyConfigurations مستند شده). فقط برای خانواده‌هایی که در Equipment واقعی/کد
/// موجود توجیه دارند (Pump/Motor/Valve/Compressor/Fan/Gearbox/HeatExchanger/PressureVessel/
/// StorageTank/Instrumentation) - طبق دستور صریح بند ۴۴ (Anti-Overengineering) از تولید
/// Mode های فرضی برای خانواده‌های بدون‌کاربرد واقعی خودداری شد.
/// باید بعد از EquipmentClassSeeder، FailureCauseSeeder و FailureEffectSeeder/
/// FailureConsequenceSeeder اجرا شود.
/// </summary>
public static class FailureModeSeeder
{
    private static readonly (string Code, string Name, string NameFa, string? EquipmentClassCode, string? EffectCode, string? ConsequenceCode, string[] CauseCodes, int Order)[] Defaults =
    {
        // ==================== Pump ====================
        ("PUMP_NO_FLOW", "No Flow / Failure to Start", "عدم جریان / عدم استارت", "PUMP", "LOSS_OF_FUNCTION", "PRODUCTION",
            new[] { "ELECTRICAL_FAULT", "OVERLOAD", "MISALIGNMENT" }, 10),
        ("PUMP_LOW_FLOW", "Reduced Flow / Pressure", "کاهش دبی / فشار", "PUMP", "REDUCED_CAPACITY", "PRODUCTION",
            new[] { "NORMAL_WEAR", "CONTAMINATION", "MISALIGNMENT" }, 20),
        ("PUMP_EXCESSIVE_VIBRATION", "Excessive Vibration", "لرزش بیش‌ازحد", "PUMP", "DEGRADED_FUNCTION", "PRODUCTION",
            new[] { "MISALIGNMENT", "NORMAL_WEAR" }, 30),
        ("PUMP_SEAL_LEAKAGE", "Seal / Packing Leakage", "نشتی سیل / پکینگ", "PUMP", "LEAKAGE", "ENVIRONMENT",
            new[] { "NORMAL_WEAR", "CORROSION", "MISALIGNMENT" }, 40),
        ("PUMP_BEARING_FAILURE", "Bearing Failure", "خرابی یاتاقان", "PUMP", "LOSS_OF_FUNCTION", "PRODUCTION",
            new[] { "LUBRICATION_FAILURE", "OVERLOAD", "CONTAMINATION" }, 50),

        // ==================== Motor ====================
        ("MOTOR_FAILS_TO_START", "Fails to Start", "عدم استارت", "MOTOR", "LOSS_OF_FUNCTION", "PRODUCTION",
            new[] { "ELECTRICAL_FAULT", "OVERLOAD" }, 60),
        ("MOTOR_OVERHEATING", "Overheating", "گرمای بیش‌ازحد", "MOTOR", "DEGRADED_FUNCTION", "PRODUCTION",
            new[] { "OVERLOAD", "ELECTRICAL_FAULT", "CONTAMINATION" }, 70),
        ("MOTOR_WINDING_INSULATION_FAILURE", "Winding Insulation Failure", "خرابی عایق سیم‌پیچی", "MOTOR", "LOSS_OF_FUNCTION", "SAFETY",
            new[] { "ELECTRICAL_FAULT", "OVERHEATING", "CONTAMINATION" }, 80),
        ("MOTOR_BEARING_FAILURE", "Bearing Failure", "خرابی یاتاقان", "MOTOR", "LOSS_OF_FUNCTION", "PRODUCTION",
            new[] { "LUBRICATION_FAILURE", "MISALIGNMENT", "NORMAL_WEAR" }, 90),

        // ==================== Valve ====================
        ("VALVE_FAILS_TO_OPERATE", "Fails to Open / Close", "عدم بازوبسته‌شدن", "VALVE", "LOSS_OF_FUNCTION", "SAFETY",
            new[] { "CORROSION", "CONTAMINATION", "MISALIGNMENT" }, 100),
        ("VALVE_EXTERNAL_LEAKAGE", "External Leakage", "نشتی بیرونی", "VALVE", "LEAKAGE", "ENVIRONMENT",
            new[] { "CORROSION", "NORMAL_WEAR" }, 110),
        ("VALVE_INTERNAL_PASSING", "Internal Leakage / Passing", "نشتی داخلی / پس‌زدن", "VALVE", "DEGRADED_FUNCTION", "PRODUCTION",
            new[] { "EROSION", "CORROSION", "FOREIGN_OBJECT" }, 120),

        // ==================== Compressor ====================
        ("COMPRESSOR_FAILS_TO_START", "Fails to Start", "عدم استارت", "COMPRESSOR", "LOSS_OF_FUNCTION", "PRODUCTION",
            new[] { "ELECTRICAL_FAULT", "LUBRICATION_FAILURE" }, 130),
        ("COMPRESSOR_LOW_DISCHARGE_PRESSURE", "Low Discharge Pressure", "افت فشار دهش", "COMPRESSOR", "REDUCED_CAPACITY", "PRODUCTION",
            new[] { "EROSION", "NORMAL_WEAR", "CONTAMINATION" }, 140),
        ("COMPRESSOR_EXCESSIVE_VIBRATION", "Excessive Vibration", "لرزش بیش‌ازحد", "COMPRESSOR", "DEGRADED_FUNCTION", "SAFETY",
            new[] { "MISALIGNMENT", "OVERLOAD" }, 150),
        ("COMPRESSOR_SURGE", "Surge", "سرج", "COMPRESSOR", "DEGRADED_FUNCTION", "PRODUCTION",
            new[] { "PROCESS_CONDITION" }, 160),

        // ==================== Fan ====================
        ("FAN_EXCESSIVE_VIBRATION", "Excessive Vibration", "لرزش بیش‌ازحد", "FAN", "DEGRADED_FUNCTION", "PRODUCTION",
            new[] { "MISALIGNMENT", "CONTAMINATION" }, 170),
        ("FAN_REDUCED_AIRFLOW", "Reduced Airflow", "کاهش دبی هوا", "FAN", "REDUCED_CAPACITY", "PRODUCTION",
            new[] { "CONTAMINATION", "NORMAL_WEAR" }, 180),
        ("FAN_BEARING_FAILURE", "Bearing Failure", "خرابی یاتاقان", "FAN", "LOSS_OF_FUNCTION", "PRODUCTION",
            new[] { "LUBRICATION_FAILURE", "NORMAL_WEAR" }, 190),

        // ==================== Gearbox ====================
        ("GEARBOX_ABNORMAL_NOISE_VIBRATION", "Abnormal Noise / Vibration", "صدا/لرزش غیرعادی", "GEARBOX", "DEGRADED_FUNCTION", "PRODUCTION",
            new[] { "NORMAL_WEAR", "MISALIGNMENT", "LUBRICATION_FAILURE" }, 200),
        ("GEARBOX_OIL_LEAKAGE", "Oil Leakage", "نشتی روغن", "GEARBOX", "LEAKAGE", "ENVIRONMENT",
            new[] { "NORMAL_WEAR", "CORROSION" }, 210),
        ("GEARBOX_GEAR_TOOTH_FAILURE", "Gear Tooth Failure", "خرابی دندانه چرخ‌دنده", "GEARBOX", "LOSS_OF_FUNCTION", "PRODUCTION",
            new[] { "OVERLOAD", "LUBRICATION_FAILURE", "FATIGUE" }, 220),

        // ==================== Heat Exchanger ====================
        ("HEAT_EXCHANGER_TUBE_LEAKAGE", "Tube Leakage", "نشتی لوله", "HEAT_EXCHANGER", "LEAKAGE", "ENVIRONMENT",
            new[] { "CORROSION", "EROSION", "FATIGUE" }, 230),
        ("HEAT_EXCHANGER_FOULING", "Fouling", "رسوب‌گذاری", "HEAT_EXCHANGER", "REDUCED_CAPACITY", "PRODUCTION",
            new[] { "CONTAMINATION", "PROCESS_CONDITION" }, 240),
        ("HEAT_EXCHANGER_REDUCED_HEAT_TRANSFER", "Reduced Heat Transfer Efficiency", "کاهش راندمان انتقال حرارت", "HEAT_EXCHANGER", "DEGRADED_FUNCTION", "QUALITY",
            new[] { "CONTAMINATION", "PROCESS_CONDITION" }, 250),

        // ==================== Pressure Vessel ====================
        ("PRESSURE_VESSEL_EXTERNAL_CORROSION", "External Corrosion", "خوردگی بیرونی", "PRESSURE_VESSEL", "DEGRADED_FUNCTION", "SAFETY",
            new[] { "CORROSION" }, 260),
        ("PRESSURE_VESSEL_INTERNAL_CORROSION", "Internal Corrosion", "خوردگی داخلی", "PRESSURE_VESSEL", "DEGRADED_FUNCTION", "SAFETY",
            new[] { "CORROSION", "PROCESS_CONDITION" }, 270),
        ("PRESSURE_VESSEL_CRACKING", "Cracking", "ترک‌خوردگی", "PRESSURE_VESSEL", "SAFETY_IMPACT", "SAFETY",
            new[] { "FATIGUE", "DESIGN_DEFICIENCY" }, 280),

        // ==================== Storage Tank ====================
        ("STORAGE_TANK_BOTTOM_CORROSION", "Bottom Corrosion", "خوردگی کف تانک", "STORAGE_TANK", "LEAKAGE", "ENVIRONMENT",
            new[] { "CORROSION" }, 290),
        ("STORAGE_TANK_ROOF_SEAL_DEGRADATION", "Roof Seal Degradation", "فرسایش سیل سقف شناور", "STORAGE_TANK", "ENVIRONMENTAL_IMPACT", "ENVIRONMENT",
            new[] { "NORMAL_WEAR" }, 300),

        // ==================== Instrumentation ====================
        ("INSTRUMENT_CALIBRATION_DRIFT", "Calibration Drift", "انحراف کالیبراسیون", "INSTRUMENTATION_EQUIPMENT", "DEGRADED_FUNCTION", "QUALITY",
            new[] { "NORMAL_WEAR", "PROCESS_CONDITION" }, 310),
        ("INSTRUMENT_SIGNAL_FAILURE", "Signal Failure", "قطع/خرابی سیگنال", "INSTRUMENTATION_EQUIPMENT", "LOSS_OF_FUNCTION", "SAFETY",
            new[] { "ELECTRICAL_FAULT", "INSTRUMENTATION_FAULT" }, 320),
        ("INSTRUMENT_SENSOR_FOULING", "Sensor Fouling", "رسوب‌گذاری روی سنسور", "INSTRUMENTATION_EQUIPMENT", "DEGRADED_FUNCTION", "QUALITY",
            new[] { "CONTAMINATION" }, 330),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var classesByCode = await db.EquipmentClasses.ToDictionaryAsync(x => x.Code, x => x.EquipmentClassId, StringComparer.OrdinalIgnoreCase);
        var effectsByCode = await db.FailureEffects.ToDictionaryAsync(x => x.Code, x => x.FailureEffectId, StringComparer.OrdinalIgnoreCase);
        var consequencesByCode = await db.FailureConsequences.ToDictionaryAsync(x => x.Code, x => x.FailureConsequenceId, StringComparer.OrdinalIgnoreCase);
        var causesByCode = await db.FailureCauses.ToDictionaryAsync(x => x.Code, x => x.FailureCauseId, StringComparer.OrdinalIgnoreCase);

        var existingCodes = (await db.FailureModes.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = Defaults.Where(d => !existingCodes.Contains(d.Code)).ToList();
        if (missing.Count > 0)
        {
            foreach (var (code, name, nameFa, classCode, effectCode, consequenceCode, _, order) in missing)
            {
                int? equipmentClassId = classCode != null && classesByCode.TryGetValue(classCode, out var clsId) ? clsId : null;
                int? effectId = effectCode != null && effectsByCode.TryGetValue(effectCode, out var effId) ? effId : null;
                int? consequenceId = consequenceCode != null && consequencesByCode.TryGetValue(consequenceCode, out var consId) ? consId : null;

                db.FailureModes.Add(new FailureMode
                {
                    Code = code,
                    Name = name,
                    NameFa = nameFa,
                    EquipmentClassId = equipmentClassId,
                    FailureEffectId = effectId,
                    FailureConsequenceId = consequenceId,
                    DisplayOrder = order,
                    IsSystemDefault = true,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                });
            }

            await db.SaveChangesAsync();
        }

        // ==================== پیوند Mode↔Cause (Many-to-Many) ====================
        var modesByCode = await db.FailureModes.ToDictionaryAsync(x => x.Code, x => x.FailureModeId, StringComparer.OrdinalIgnoreCase);
        var existingLinks = (await db.FailureModeCauses
                .Select(x => new { x.FailureModeId, x.FailureCauseId })
                .ToListAsync())
            .Select(x => (x.FailureModeId, x.FailureCauseId))
            .ToHashSet();

        var linksAdded = false;

        foreach (var (code, _, _, _, _, _, causeCodes, _) in Defaults)
        {
            if (!modesByCode.TryGetValue(code, out var modeId)) continue;

            foreach (var causeCode in causeCodes)
            {
                if (!causesByCode.TryGetValue(causeCode, out var causeId)) continue;
                if (existingLinks.Contains((modeId, causeId))) continue;

                db.FailureModeCauses.Add(new FailureModeCause { FailureModeId = modeId, FailureCauseId = causeId });
                existingLinks.Add((modeId, causeId));
                linksAdded = true;
            }
        }

        if (linksAdded)
            await db.SaveChangesAsync();
    }
}
