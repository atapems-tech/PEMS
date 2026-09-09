using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS — Enterprise Maintenance Master Data &amp; Standards Implementation» بند ۸ (فاز ۱)
/// و «PEMS Phase 2 — Equipment Classification» بند ۶: خط‌مبنای طبقه‌بندی تجهیزات، حالا با
/// سلسله‌مراتب اختیاری (Rotating Equipment → Pump → Centrifugal Pump). ۱۵ کلاس فاز ۱
/// (PUMP_CENT/COMPRESSOR/MOTOR/...) هرگز تغییرنام یا حذف نشدند - چون Activity Applicability
/// فاز ۱ (MaintenanceActivitySeeder) دقیقاً همین کدها را ارجاع می‌دهد. فقط یک والد برایشان
/// اضافه شد (فقط وقتی ParentEquipmentClassId فعلاً Null است - هرگز یک انتساب دستی کاربر را
/// بازنویسی نمی‌کند، طبق بند ۳۰: «Master Data Protection»).
/// </summary>
public static class EquipmentClassSeeder
{
    private static readonly (string Code, string Name, string NameFa, string? ParentCode, int Order)[] Defaults =
    {
        // ==================== Phase 1 (بدون تغییر کد/نام) ====================
        ("PUMP_CENT", "Centrifugal Pump", "پمپ سانتریفیوژ", "PUMP", 10),
        ("PUMP_RECIP", "Reciprocating Pump", "پمپ رفت‌وبرگشتی", "PUMP", 20),
        ("COMPRESSOR", "Compressor", "کمپرسور", "ROTATING_EQUIPMENT", 30),
        ("FAN", "Fan", "فن", "ROTATING_EQUIPMENT", 40),
        ("BLOWER", "Blower", "بلوئر", "ROTATING_EQUIPMENT", 50),
        ("GEARBOX", "Gearbox", "گیربکس", "ROTATING_EQUIPMENT", 60),
        ("TURBINE", "Turbine", "توربین", "ROTATING_EQUIPMENT", 70),
        ("MOTOR", "Electric Motor", "موتور الکتریکی", "ELECTRICAL_EQUIPMENT", 80),
        ("PRESSURE_VESSEL", "Pressure Vessel", "مخزن تحت فشار", "STATIC_EQUIPMENT", 90),
        ("HEAT_EXCHANGER", "Heat Exchanger", "مبدل حرارتی", "STATIC_EQUIPMENT", 100),
        ("STORAGE_TANK", "Storage Tank", "تانک ذخیره", "STATIC_EQUIPMENT", 110),
        ("PIPING", "Piping", "خط لوله", null, 120),
        ("VALVE", "Valve", "شیر (والو)", "PIPING", 130),
        ("TRANSFORMER", "Transformer", "ترانسفورماتور", "ELECTRICAL_EQUIPMENT", 140),
        ("SWITCHGEAR", "Switchgear", "سوئیچ‌گیر", "ELECTRICAL_EQUIPMENT", 150),

        // ==================== Phase 2: دسته‌های بالادستی جدید ====================
        ("ROTATING_EQUIPMENT", "Rotating Equipment", "تجهیزات دوار", null, 1),
        ("STATIC_EQUIPMENT", "Static Equipment", "تجهیزات ثابت", null, 2),
        ("ELECTRICAL_EQUIPMENT", "Electrical Equipment", "تجهیزات برقی", null, 3),
        ("INSTRUMENTATION_EQUIPMENT", "Instrumentation Equipment", "تجهیزات ابزار دقیق", null, 4),
        ("FIRE_GAS_EQUIPMENT", "Fire & Gas Equipment", "تجهیزات آتش و گاز", null, 5),
        ("HVAC_EQUIPMENT", "HVAC Equipment", "تجهیزات تهویه مطبوع", null, 6),
        ("PUMP", "Pump", "پمپ", "ROTATING_EQUIPMENT", 15),

        // ==================== Phase 2: زیرمجموعه‌های جدید ====================
        ("PUMP_PD", "Positive Displacement Pump", "پمپ جابجایی مثبت", "PUMP", 25),
        ("COMPRESSOR_CENTRIFUGAL", "Centrifugal Compressor", "کمپرسور سانتریفیوژ", "COMPRESSOR", 31),
        ("COMPRESSOR_RECIP", "Reciprocating Compressor", "کمپرسور رفت‌وبرگشتی", "COMPRESSOR", 32),
        ("COMPRESSOR_SCREW", "Screw Compressor", "کمپرسور اسکرو", "COMPRESSOR", 33),

        ("COLUMN", "Column", "برج", "STATIC_EQUIPMENT", 111),
        ("REACTOR", "Reactor", "راکتور", "STATIC_EQUIPMENT", 112),
        ("FILTER", "Filter", "فیلتر", "STATIC_EQUIPMENT", 113),

        ("PROCESS_PIPING", "Process Piping", "خط لوله فرآیندی", "PIPING", 121),
        ("UTILITY_PIPING", "Utility Piping", "خط لوله سرویس‌های جانبی", "PIPING", 122),
        ("PIPELINE", "Pipeline", "خط لوله انتقال", "PIPING", 123),

        ("GENERATOR", "Generator", "ژنراتور", "ELECTRICAL_EQUIPMENT", 141),
        ("MCC", "Motor Control Center", "مرکز کنترل موتور (MCC)", "ELECTRICAL_EQUIPMENT", 142),
        ("UPS", "UPS", "یو‌پی‌اس", "ELECTRICAL_EQUIPMENT", 143),
        ("BATTERY", "Battery", "باتری", "ELECTRICAL_EQUIPMENT", 144),

        ("PRESSURE_TRANSMITTER", "Pressure Transmitter", "ترانسمیتر فشار", "INSTRUMENTATION_EQUIPMENT", 210),
        ("TEMPERATURE_TRANSMITTER", "Temperature Transmitter", "ترانسمیتر دما", "INSTRUMENTATION_EQUIPMENT", 220),
        ("FLOW_TRANSMITTER", "Flow Transmitter", "ترانسمیتر دبی", "INSTRUMENTATION_EQUIPMENT", 230),
        ("LEVEL_TRANSMITTER", "Level Transmitter", "ترانسمیتر سطح", "INSTRUMENTATION_EQUIPMENT", 240),
        ("CONTROL_VALVE_EQ", "Control Valve", "شیر کنترل", "INSTRUMENTATION_EQUIPMENT", 250),
        ("ON_OFF_VALVE", "On/Off Valve", "شیر روشن/خاموش", "INSTRUMENTATION_EQUIPMENT", 260),
        ("ANALYZER_EQ", "Analyzer", "آنالایزر", "INSTRUMENTATION_EQUIPMENT", 270),
        ("PLC_DCS", "PLC / DCS", "پی‌ال‌سی / دی‌سی‌اس", "INSTRUMENTATION_EQUIPMENT", 280),

        ("FIRE_DETECTOR", "Fire Detector", "آشکارساز آتش", "FIRE_GAS_EQUIPMENT", 310),
        ("GAS_DETECTOR", "Gas Detector", "آشکارساز گاز", "FIRE_GAS_EQUIPMENT", 320),
        ("FIRE_PUMP", "Fire Pump", "پمپ آتش‌نشانی", "FIRE_GAS_EQUIPMENT", 330),
        ("FIRE_WATER_SYSTEM", "Fire Water System", "سیستم آب آتش‌نشانی", "FIRE_GAS_EQUIPMENT", 340),
        ("FIRE_ALARM_PANEL", "Fire Alarm Panel", "پنل اعلام حریق", "FIRE_GAS_EQUIPMENT", 350),

        ("AHU", "Air Handling Unit (AHU)", "دستگاه هواساز (AHU)", "HVAC_EQUIPMENT", 410),
        ("CHILLER", "Chiller", "چیلر", "HVAC_EQUIPMENT", 420),
        ("PACKAGE_UNIT", "Package Unit", "پکیج یونیت", "HVAC_EQUIPMENT", 430),
        ("EXHAUST_FAN", "Exhaust Fan", "فن تخلیه هوا", "HVAC_EQUIPMENT", 440),

        // طبق فاز ۲.۳: افزوده شده پس از بررسی داده واقعی Equipment.EquipmentType در دیتابیس
        // زنده (drum/mixer/agitator/conveyor به تعداد قابل توجه یافت شدند - نه فرضی/ساختگی)
        ("MIXER_AGITATOR", "Mixer / Agitator", "میکسر / اجیتاتور", "ROTATING_EQUIPMENT", 71),
        ("CONVEYOR", "Conveyor", "نوار نقاله", "ROTATING_EQUIPMENT", 72),

        // ==============================================================================
        // طبق «PEMS Phase 3 — Equipment Classification Completion» بند ۸: پروفایل مستقیم
        // داده‌ی زنده‌ی Equipment.Name برای EquipmentType='DEVICE'/'F&G DEVICE' نشان داد این دو
        // نوع عمومی در واقع فقط ۱۶۳/۱۵ مقدار Name متمایز دارند که هرکدام یک نوع ابزاردقیق/آتش‌ونشانی
        // مشخص و صددرصد قابل‌تفسیر است (مثلاً "THERMOWELL" با ۱۳۱۸ رکورد واقعی) - نه یک حدس، بلکه
        // استخراج مستقیم از خودِ داده. این ۱۵ کلاس جدید فقط برای Name هایی اضافه شدند که تعداد
        // رکورد واقعی‌شان (≥۳۰) توجیه‌کننده بود؛ دنباله‌ی کم‌تکرار (Long Tail) عمداً بدون کلاس
        // جدید می‌ماند تا در صف بازبینی انسانی (Review Queue) بماند - طبق بند ۳۱: «Do NOT define
        // 100% coverage as a target».
        // ==============================================================================
        ("THERMOWELL", "Thermowell", "ترموول", "INSTRUMENTATION_EQUIPMENT", 290),
        ("PRESSURE_GAUGE", "Pressure Gauge", "فشارسنج عقربه‌ای", "INSTRUMENTATION_EQUIPMENT", 291),
        ("TEMPERATURE_GAUGE", "Temperature Gauge", "دماسنج عقربه‌ای", "INSTRUMENTATION_EQUIPMENT", 292),
        ("RTD_ELEMENT", "RTD Element", "المان RTD", "INSTRUMENTATION_EQUIPMENT", 293),
        ("LEVEL_GAUGE", "Level Gauge", "سطح‌سنج عینکی", "INSTRUMENTATION_EQUIPMENT", 294),
        ("LEVEL_SWITCH", "Level Switch", "سوئیچ سطح", "INSTRUMENTATION_EQUIPMENT", 295),
        ("PRESSURE_SWITCH", "Pressure Switch", "سوئیچ فشار", "INSTRUMENTATION_EQUIPMENT", 296),
        ("FLOW_SWITCH", "Flow Switch", "سوئیچ دبی", "INSTRUMENTATION_EQUIPMENT", 297),
        ("LIMIT_SWITCH", "Limit / Position Switch", "سوئیچ وضعیت (لیمیت)", "INSTRUMENTATION_EQUIPMENT", 298),
        ("HAND_SWITCH", "Hand Switch", "سوئیچ دستی", "INSTRUMENTATION_EQUIPMENT", 299),
        ("FLOW_ELEMENT", "Flow Element (Orifice)", "المان دبی (اوریفیس)", "INSTRUMENTATION_EQUIPMENT", 300),
        ("ROTAMETER", "Rotameter", "روتامتر", "INSTRUMENTATION_EQUIPMENT", 301),
        ("PRESSURE_SAFETY_VALVE", "Pressure Safety Valve", "شیر اطمینان فشار (PSV)", "PIPING", 131),
        ("MANUAL_CALL_POINT", "Manual Call Point", "شستی اعلام حریق دستی", "FIRE_GAS_EQUIPMENT", 351),
        ("ALARM_NOTIFICATION_DEVICE", "Alarm Notification Device (Hooter/Flasher)", "دستگاه اعلام هشدار (هوتر/چراغ چشمک‌زن)", "FIRE_GAS_EQUIPMENT", 352),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        // ==================== گام ۱: افزودن رکوردهای غایب (بدون Parent برای این گام) ====================
        var existingCodes = (await db.EquipmentClasses.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = Defaults.Where(d => !existingCodes.Contains(d.Code)).ToList();
        if (missing.Count > 0)
        {
            foreach (var (code, name, nameFa, _, order) in missing)
            {
                db.EquipmentClasses.Add(new EquipmentClass
                {
                    Code = code,
                    Name = name,
                    NameFa = nameFa,
                    DisplayOrder = order,
                    IsSystemDefault = true,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                });
            }

            await db.SaveChangesAsync();
        }

        // ==============================================================================
        // گام ۲: تعیین والد فقط برای رکوردهایی که هنوز ParentEquipmentClassId=Null دارند -
        // طبق بند ۳۰ («Master Data Protection») هرگز یک انتساب دستی/موجود کاربر را بازنویسی
        // نمی‌کند؛ فقط خلأ را پر می‌کند - هم برای ۱۵ کلاس فاز ۱ (که تا امروز والد نداشتند) هم
        // برای هر رکورد جدید همین اجرا.
        // ==============================================================================
        var allClasses = await db.EquipmentClasses.ToListAsync();
        var byCode = allClasses.ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var (code, _, _, parentCode, _) in Defaults)
        {
            if (parentCode == null) continue;
            if (!byCode.TryGetValue(code, out var entity)) continue;
            if (entity.ParentEquipmentClassId.HasValue) continue; // قبلاً تعیین شده - دست نمی‌زنیم
            if (!byCode.TryGetValue(parentCode, out var parent)) continue;

            entity.ParentEquipmentClassId = parent.EquipmentClassId;
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync();
    }
}
