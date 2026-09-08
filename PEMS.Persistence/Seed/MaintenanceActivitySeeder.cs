using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS — Enterprise Maintenance Master Data &amp; Standards Implementation» بند ۷/۸/۱۱:
/// کاتالوگ فعالیت‌های استاندارد + کاربردپذیری داده‌محور (Equipment Class / Strategy Type /
/// Standard Reference) - دقیقاً همون مثال‌های سند (Rotating/Electrical/Instrumentation/Static/
/// Piping/Lubrication). این کاتالوگ عمداً از MaintenanceTask/JobPlan (که قالب اجرای قابل‌استفاده‌ی
/// مجدد برای یک WorkOrder خاص است) جدا نگه داشته شده - این‌جا تاکسونومی مرجع است.
/// اجرا باید بعد از MaintenanceGroupSeeder/MaintenanceTypeSeeder/MaintenanceStrategyTypeSeeder/
/// EquipmentClassSeeder/StandardReferenceSeeder باشد (به کدهای آن‌ها ارجاع می‌دهد).
/// </summary>
public static class MaintenanceActivitySeeder
{
    private sealed record ActivityDef(
        string Code, string Name, string NameFa, string GroupCode, string? TypeCode,
        string[] EquipmentClassCodes, string[] StrategyTypeCodes, string[] StandardCodes);

    private static readonly ActivityDef[] Defaults =
    {
        // ===================== Rotating Equipment (MECH-ROT) =====================
        new("ACT_VIBRATION_MEASUREMENT", "Vibration Measurement", "اندازه‌گیری ارتعاش", "MECH-ROT", "CONDITION_BASED",
            new[] { "PUMP_CENT", "COMPRESSOR", "FAN", "BLOWER", "GEARBOX", "TURBINE", "MOTOR" }, new[] { "PDM", "CBM" }, new[] { "ISO-14224" }),
        new("ACT_VIBRATION_ANALYSIS", "Vibration Analysis", "آنالیز ارتعاش", "MECH-ROT", "PREDICTIVE",
            new[] { "PUMP_CENT", "COMPRESSOR", "FAN", "BLOWER", "GEARBOX", "TURBINE", "MOTOR" }, new[] { "PDM", "CBM" }, new[] { "ISO-14224" }),
        new("ACT_SHAFT_ALIGNMENT", "Shaft Alignment", "هم‌راستاسازی شافت", "MECH-ROT", "ALIGNMENT",
            new[] { "PUMP_CENT", "PUMP_RECIP", "COMPRESSOR", "GEARBOX", "TURBINE", "MOTOR" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_COUPLING_INSPECTION", "Coupling Inspection", "بازرسی کوپلینگ", "MECH-ROT", "INSPECTION",
            new[] { "PUMP_CENT", "PUMP_RECIP", "COMPRESSOR", "GEARBOX", "TURBINE", "MOTOR" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_BEARING_INSPECTION", "Bearing Inspection", "بازرسی بلبرینگ", "MECH-ROT", "INSPECTION",
            new[] { "PUMP_CENT", "PUMP_RECIP", "COMPRESSOR", "FAN", "BLOWER", "GEARBOX", "TURBINE", "MOTOR" }, new[] { "PM", "PDM" }, Array.Empty<string>()),
        new("ACT_BEARING_REPLACEMENT", "Bearing Replacement", "تعویض بلبرینگ", "MECH-ROT", "CORRECTIVE",
            new[] { "PUMP_CENT", "PUMP_RECIP", "COMPRESSOR", "FAN", "BLOWER", "GEARBOX", "TURBINE", "MOTOR" }, new[] { "CM" }, Array.Empty<string>()),
        new("ACT_MECH_SEAL_INSPECTION", "Mechanical Seal Inspection", "بازرسی سیل مکانیکی", "MECH-ROT", "INSPECTION",
            new[] { "PUMP_CENT", "PUMP_RECIP", "COMPRESSOR" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_MECH_SEAL_REPLACEMENT", "Mechanical Seal Replacement", "تعویض سیل مکانیکی", "MECH-ROT", "CORRECTIVE",
            new[] { "PUMP_CENT", "PUMP_RECIP", "COMPRESSOR" }, new[] { "CM" }, Array.Empty<string>()),
        new("ACT_ROTATING_LUBRICATION", "Lubrication (Rotating Equipment)", "روانکاری تجهیزات دوّار", "MECH-ROT", "LUBRICATION",
            new[] { "PUMP_CENT", "PUMP_RECIP", "COMPRESSOR", "FAN", "BLOWER", "GEARBOX", "TURBINE", "MOTOR" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_IMPELLER_INSPECTION", "Impeller Inspection", "بازرسی پروانه", "MECH-ROT", "INSPECTION",
            new[] { "PUMP_CENT", "PUMP_RECIP" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_IMPELLER_REPLACEMENT", "Impeller Replacement", "تعویض پروانه", "MECH-ROT", "CORRECTIVE",
            new[] { "PUMP_CENT", "PUMP_RECIP" }, new[] { "CM" }, Array.Empty<string>()),
        new("ACT_PERFORMANCE_TEST", "Performance Test", "تست عملکرد", "MECH-ROT", "TESTING",
            new[] { "PUMP_CENT", "PUMP_RECIP", "COMPRESSOR", "FAN", "BLOWER", "TURBINE" }, new[] { "PDM" }, Array.Empty<string>()),
        new("ACT_ROTATING_OVERHAUL", "Overhaul (Rotating Equipment)", "بازسازی کامل تجهیزات دوّار", "MECH-ROT", "OVERHAUL",
            new[] { "PUMP_CENT", "PUMP_RECIP", "COMPRESSOR", "GEARBOX", "TURBINE", "MOTOR" }, new[] { "OVERHAUL" }, Array.Empty<string>()),
        new("ACT_BALANCING", "Balancing", "بالانس کردن", "MECH-ROT", "ALIGNMENT",
            new[] { "FAN", "BLOWER", "TURBINE", "MOTOR" }, new[] { "PM" }, Array.Empty<string>()),

        // ===================== Electrical =====================
        new("ACT_INSULATION_RESISTANCE_TEST", "Insulation Resistance Test", "تست مقاومت عایقی", "ELEC-MOT", "TESTING",
            new[] { "MOTOR", "TRANSFORMER" }, new[] { "PDM" }, Array.Empty<string>()),
        new("ACT_WINDING_RESISTANCE_TEST", "Winding Resistance Test", "تست مقاومت سیم‌پیچی", "ELEC-MOT", "TESTING",
            new[] { "MOTOR", "TRANSFORMER" }, new[] { "PDM" }, Array.Empty<string>()),
        new("ACT_THERMOGRAPHY", "Thermography", "ترموگرافی", "ELEC-MOT", "CONDITION_BASED",
            new[] { "MOTOR", "TRANSFORMER", "SWITCHGEAR" }, new[] { "CBM", "PDM" }, Array.Empty<string>()),
        new("ACT_CURRENT_MEASUREMENT", "Current Measurement", "اندازه‌گیری جریان", "ELEC-MOT", "TESTING",
            new[] { "MOTOR" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_VOLTAGE_MEASUREMENT", "Voltage Measurement", "اندازه‌گیری ولتاژ", "ELEC-PWR", "TESTING",
            new[] { "MOTOR", "TRANSFORMER" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_EARTHING_TEST", "Earthing Test", "تست ارتینگ", "ELEC-PWR", "TESTING",
            Array.Empty<string>(), new[] { "PM" }, Array.Empty<string>()),
        new("ACT_TERMINAL_INSPECTION", "Terminal Inspection", "بازرسی ترمینال", "ELEC-MOT", "INSPECTION",
            new[] { "MOTOR", "SWITCHGEAR" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_CABLE_INSPECTION", "Cable Inspection", "بازرسی کابل", "ELEC-CAB", "INSPECTION",
            Array.Empty<string>(), new[] { "PM" }, Array.Empty<string>()),
        new("ACT_MOTOR_INSPECTION", "Motor Inspection", "بازرسی موتور", "ELEC-MOT", "INSPECTION",
            new[] { "MOTOR" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_MOTOR_OVERHAUL", "Motor Overhaul", "بازسازی موتور", "ELEC-MOT", "OVERHAUL",
            new[] { "MOTOR" }, new[] { "OVERHAUL" }, Array.Empty<string>()),
        new("ACT_PROTECTION_RELAY_TEST", "Protection Relay Test", "تست رله حفاظتی", "ELEC-PRO", "FUNCTIONAL_TEST",
            new[] { "SWITCHGEAR" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_BREAKER_FUNCTIONAL_TEST", "Breaker Functional Test", "تست عملکردی بریکر", "ELEC-SWG", "FUNCTIONAL_TEST",
            new[] { "SWITCHGEAR" }, new[] { "PM" }, Array.Empty<string>()),

        // ===================== Instrumentation =====================
        new("ACT_INST_CALIBRATION", "Calibration", "کالیبراسیون", "INST-CAL", "CALIBRATION",
            Array.Empty<string>(), new[] { "PM" }, Array.Empty<string>()),
        new("ACT_LOOP_CHECK", "Loop Check", "بررسی حلقه", "INST-FLD", "FUNCTIONAL_TEST",
            Array.Empty<string>(), new[] { "PM" }, Array.Empty<string>()),
        new("ACT_INST_FUNCTIONAL_TEST", "Functional Test (Instrument)", "تست عملکردی ابزاردقیق", "INST-FLD", "FUNCTIONAL_TEST",
            Array.Empty<string>(), new[] { "PM" }, Array.Empty<string>()),
        new("ACT_POSITIONER_CALIBRATION", "Positioner Calibration", "کالیبراسیون پوزیشنر", "INST-CV", "CALIBRATION",
            new[] { "VALVE" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_TRANSMITTER_CALIBRATION", "Transmitter Calibration", "کالیبراسیون ترانسمیتر", "INST-CAL", "CALIBRATION",
            Array.Empty<string>(), new[] { "PM" }, Array.Empty<string>()),
        new("ACT_CONTROL_VALVE_INSPECTION", "Control Valve Inspection", "بازرسی شیر کنترل", "INST-CV", "INSPECTION",
            new[] { "VALVE" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_INST_LEAKAGE_TEST", "Leakage Test (Instrument)", "تست نشتی ابزاردقیق", "INST-CV", "TESTING",
            new[] { "VALVE" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_ACTUATOR_INSPECTION", "Actuator Inspection", "بازرسی محرک (اکچویتور)", "INST-CV", "INSPECTION",
            new[] { "VALVE" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_SIGNAL_VERIFICATION", "Signal Verification", "تایید سیگنال", "INST-FLD", "TESTING",
            Array.Empty<string>(), new[] { "PM" }, Array.Empty<string>()),
        new("ACT_ALARM_TEST", "Alarm Test", "تست آلارم", "INST-SIS", "FUNCTIONAL_TEST",
            Array.Empty<string>(), new[] { "PM" }, Array.Empty<string>()),
        new("ACT_INTERLOCK_TEST", "Interlock Test", "تست اینترلاک", "INST-SIS", "FUNCTIONAL_TEST",
            Array.Empty<string>(), new[] { "PM" }, Array.Empty<string>()),
        new("ACT_TRIP_TEST", "Trip Test", "تست تریپ", "INST-SIS", "FUNCTIONAL_TEST",
            Array.Empty<string>(), new[] { "PM" }, Array.Empty<string>()),

        // ===================== Static Equipment (MECH-STA) =====================
        new("ACT_STATIC_VISUAL_INSPECTION", "Visual Inspection (Static Equipment)", "بازرسی چشمی تجهیزات ثابت", "MECH-STA", "INSPECTION",
            new[] { "PRESSURE_VESSEL", "HEAT_EXCHANGER", "STORAGE_TANK" }, new[] { "PM" }, new[] { "API-510", "API-653" }),
        new("ACT_THICKNESS_MEASUREMENT", "Thickness Measurement", "اندازه‌گیری ضخامت", "MECH-STA", "INSPECTION",
            new[] { "PRESSURE_VESSEL", "HEAT_EXCHANGER", "STORAGE_TANK", "PIPING" }, new[] { "PDM" }, new[] { "API-510", "API-570", "API-653", "API-581" }),
        new("ACT_INTERNAL_INSPECTION", "Internal Inspection", "بازرسی داخلی", "MECH-STA", "INSPECTION",
            new[] { "PRESSURE_VESSEL", "STORAGE_TANK" }, new[] { "PM" }, new[] { "API-510", "API-653" }),
        new("ACT_EXTERNAL_INSPECTION", "External Inspection", "بازرسی خارجی", "MECH-STA", "INSPECTION",
            new[] { "PRESSURE_VESSEL", "STORAGE_TANK", "PIPING" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_NDT", "Non-Destructive Testing (NDT)", "آزمون غیرمخرب", "INSP-NDT", "TESTING",
            new[] { "PRESSURE_VESSEL", "HEAT_EXCHANGER", "PIPING" }, new[] { "PDM" }, new[] { "API-571" }),
        new("ACT_PRESSURE_TEST", "Pressure Test", "تست فشار", "MECH-STA", "TESTING",
            new[] { "PRESSURE_VESSEL" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_LEAK_TEST", "Leak Test", "تست نشتی", "PIPE-RPR", "TESTING",
            new[] { "PRESSURE_VESSEL", "PIPING", "VALVE" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_RELIEF_DEVICE_INSPECTION", "Relief Device Inspection", "بازرسی شیر اطمینان", "MECH-STA", "INSPECTION",
            new[] { "PRESSURE_VESSEL" }, new[] { "PM" }, new[] { "API-576" }),
        new("ACT_MANWAY_INSPECTION", "Manway Inspection", "بازرسی منوی", "MECH-STA", "INSPECTION",
            new[] { "PRESSURE_VESSEL", "STORAGE_TANK" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_CORROSION_ASSESSMENT", "Corrosion Assessment", "ارزیابی خوردگی", "CORR-MGT", "INSPECTION",
            new[] { "PRESSURE_VESSEL", "PIPING", "STORAGE_TANK" }, new[] { "PDM" }, new[] { "API-571", "API-580", "API-581", "AMPP" }),
        new("ACT_DAMAGE_MECHANISM_ASSESSMENT", "Damage Mechanism Assessment", "ارزیابی مکانیزم آسیب", "INSP-MI", "INSPECTION",
            new[] { "PRESSURE_VESSEL", "PIPING", "STORAGE_TANK" }, new[] { "PDM" }, new[] { "API-571" }),

        // ===================== Piping =====================
        new("ACT_FLANGE_INSPECTION", "Flange Inspection", "بازرسی فلنج", "PIPE-FLG", "INSPECTION",
            new[] { "PIPING" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_PIPING_VALVE_INSPECTION", "Valve Inspection (Piping)", "بازرسی شیر خط لوله", "PIPE-FLG", "INSPECTION",
            new[] { "VALVE" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_SUPPORT_INSPECTION", "Support Inspection", "بازرسی ساپورت", "PIPE-SUP", "INSPECTION",
            new[] { "PIPING" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_PIPE_RACK_INSPECTION", "Pipe Rack Inspection", "بازرسی پایپ‌رک", "PIPE-SUP", "INSPECTION",
            new[] { "PIPING" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_INSULATION_INSPECTION", "Insulation Inspection", "بازرسی عایق", "PIPE-SUP", "INSPECTION",
            new[] { "PIPING", "PRESSURE_VESSEL", "STORAGE_TANK" }, new[] { "PM" }, Array.Empty<string>()),

        // ===================== Lubrication (MECH-LUBE) =====================
        new("ACT_GREASING", "Greasing", "گریسکاری", "MECH-LUBE", "LUBRICATION",
            new[] { "PUMP_CENT", "PUMP_RECIP", "MOTOR", "GEARBOX", "FAN", "BLOWER" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_OIL_CHANGE", "Oil Change", "تعویض روغن", "MECH-LUBE", "LUBRICATION",
            new[] { "GEARBOX", "COMPRESSOR", "TURBINE" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_OIL_SAMPLING", "Oil Sampling", "نمونه‌برداری روغن", "MECH-LUBE", "CONDITION_BASED",
            new[] { "GEARBOX", "COMPRESSOR", "TURBINE" }, new[] { "PDM", "CBM" }, Array.Empty<string>()),
        new("ACT_OIL_ANALYSIS", "Oil Analysis", "آنالیز روغن", "MECH-LUBE", "CONDITION_BASED",
            new[] { "GEARBOX", "COMPRESSOR", "TURBINE" }, new[] { "PDM", "CBM" }, Array.Empty<string>()),
        new("ACT_LUBRICANT_INSPECTION", "Lubricant Inspection", "بازرسی روان‌کننده", "MECH-LUBE", "INSPECTION",
            new[] { "GEARBOX", "COMPRESSOR", "TURBINE" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_LUBRICANT_REPLACEMENT", "Lubricant Replacement", "تعویض روان‌کننده", "MECH-LUBE", "LUBRICATION",
            new[] { "GEARBOX", "COMPRESSOR", "TURBINE" }, new[] { "PM" }, Array.Empty<string>()),
        new("ACT_CONTAMINATION_CHECK", "Contamination Check", "بررسی آلودگی", "MECH-LUBE", "TESTING",
            new[] { "GEARBOX", "COMPRESSOR", "TURBINE" }, new[] { "PDM", "CBM" }, Array.Empty<string>()),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var groups = await db.MaintenanceGroups.Select(x => new { x.MaintenanceGroupId, x.Code }).ToListAsync();
        if (groups.Count == 0) return;

        var types = await db.MaintenanceTypes.Select(x => new { x.MaintenanceTypeId, x.Code }).ToListAsync();
        var strategyTypes = await db.MaintenanceStrategyTypes.Select(x => new { x.MaintenanceStrategyTypeId, x.Code }).ToListAsync();
        var equipmentClasses = await db.EquipmentClasses.Select(x => new { x.EquipmentClassId, x.Code }).ToListAsync();
        var standards = await db.StandardReferences.Select(x => new { x.StandardReferenceId, x.Code }).ToListAsync();

        var existingCodes = (await db.MaintenanceActivities.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var def in Defaults)
        {
            if (existingCodes.Contains(def.Code)) continue;

            var group = groups.FirstOrDefault(x => x.Code == def.GroupCode);
            if (group == null) continue; // طبق بند ۱۹: بدون Group معتبر، فعالیت ساخته نمی‌شود (No Orphans)

            var activity = new MaintenanceActivity
            {
                Code = def.Code,
                Name = def.Name,
                NameFa = def.NameFa,
                MaintenanceGroupId = group.MaintenanceGroupId,
                MaintenanceTypeId = def.TypeCode == null ? null : types.FirstOrDefault(x => x.Code == def.TypeCode)?.MaintenanceTypeId,
                IsSystemDefault = true,
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            foreach (var ecCode in def.EquipmentClassCodes)
            {
                var ec = equipmentClasses.FirstOrDefault(x => x.Code == ecCode);
                if (ec != null)
                    activity.ApplicableEquipmentClasses.Add(new MaintenanceActivityEquipmentClass { EquipmentClassId = ec.EquipmentClassId });
            }

            foreach (var stCode in def.StrategyTypeCodes)
            {
                var st = strategyTypes.FirstOrDefault(x => x.Code == stCode);
                if (st != null)
                    activity.ApplicableStrategyTypes.Add(new MaintenanceActivityStrategyType { MaintenanceStrategyTypeId = st.MaintenanceStrategyTypeId });
            }

            foreach (var stdCode in def.StandardCodes)
            {
                var std = standards.FirstOrDefault(x => x.Code == stdCode);
                if (std != null)
                    activity.ApplicableStandardReferences.Add(new MaintenanceActivityStandardReference { StandardReferenceId = std.StandardReferenceId });
            }

            db.MaintenanceActivities.Add(activity);
        }

        await db.SaveChangesAsync();
    }
}
