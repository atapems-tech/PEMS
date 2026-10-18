using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS — Enterprise Maintenance Master Data &amp; Standards Implementation» بند ۲/۱۰: فقط
/// ابرداده‌ی استانداردها (سازمان/شماره/عنوان/دسته/توضیح کاربردی کوتاه) - هیچ متن دارای Copyright
/// خودِ استاندارد این‌جا ذخیره نشده است؛ Description ها صرفاً توضیح می‌دهند این استاندارد برای
/// چه کاربردی در PEMS مرجع قرار گرفته، نه محتوای استاندارد.
/// </summary>
public static class StandardReferenceSeeder
{
    private static readonly (string Code, string Org, string Number, string Title, string Category, string Description)[] Defaults =
    {
        ("ISO-14224", "ISO", "14224", "Collection and exchange of reliability and maintenance data for equipment",
            "Reliability Data", "مرجع طبقه‌بندی Failure Mode/Cause و ساختار جمع‌آوری داده‌ی نگهداری - همون واژگانی که WorkOrder.FailureModeCode/FailureCauseCode در PEMS استفاده می‌کنند."),
        ("IEC-60300", "IEC", "60300", "Dependability management",
            "Dependability Management", "چارچوب مدیریت Dependability (قابلیت اطمینان/نت‌پذیری/پشتیبان‌پذیری) - مرجع مفهومی برای طراحی استراتژی نگهداری."),
        ("API-580", "API", "RP 580", "Risk-Based Inspection",
            "Inspection / RBI", "چارچوب کلی برنامه‌ریزی بازرسی مبتنی بر ریسک (RBI) برای تجهیزات ثابت."),
        ("API-581", "API", "RP 581", "Risk-Based Inspection Methodology",
            "Inspection / RBI", "روش‌شناسی محاسباتی RBI (احتمال و پیامد خرابی) - مکمل API RP 580."),
        ("API-510", "API", "510", "Pressure Vessel Inspection Code",
            "Inspection / Static Equipment", "مرجع بازرسی، تعمیر، تغییر و ارزیابی مجدد مخازن تحت فشار."),
        ("API-570", "API", "570", "Piping Inspection Code",
            "Inspection / Piping", "مرجع بازرسی، تعمیر، تغییر و ارزیابی مجدد سیستم‌های لوله‌کشی در سرویس."),
        ("API-653", "API", "653", "Tank Inspection, Repair, Alteration, and Reconstruction",
            "Inspection / Storage Tanks", "مرجع بازرسی و تعمیر تانک‌های ذخیره‌ی اتمسفریک فولادی."),
        ("API-576", "API", "576", "Inspection of Pressure-Relieving Devices",
            "Inspection / Relief Devices", "مرجع بازرسی و تست دوره‌ای شیرهای اطمینان فشار (PSV/PRV)."),
        ("API-571", "API", "571", "Damage Mechanisms Affecting Fixed Equipment in the Refining Industry",
            "Damage Mechanisms", "مرجع طبقه‌بندی مکانیزم‌های آسیب (خوردگی، ترک، خزش و ...) برای تجهیزات ثابت - ورودی مهم برای RBI."),
        ("ASME", "ASME", "General", "American Society of Mechanical Engineers codes and standards",
            "Mechanical / Pressure Equipment", "خانواده‌ی استانداردهای مکانیکی/تجهیزات تحت فشار (مثلاً ASME Section VIII برای مخازن، B31.3 برای Piping) - به‌عنوان دسته‌ی کلی مرجع، نه یک سند واحد."),
        ("IEC", "IEC", "General", "International Electrotechnical Commission standards",
            "Electrical / Instrumentation", "خانواده‌ی استانداردهای الکتریکی/کنترل (مثلاً IEC 61508 برای SIS) - به‌عنوان دسته‌ی کلی مرجع."),
        ("ISA", "ISA", "General", "International Society of Automation standards",
            "Instrumentation & Control", "خانواده‌ی استانداردهای ابزاردقیق/اتوماسیون (مثلاً ISA-5.1 نمادگذاری) - به‌عنوان دسته‌ی کلی مرجع."),
        ("NFPA", "NFPA", "General", "National Fire Protection Association standards",
            "Fire Protection", "خانواده‌ی استانداردهای حفاظت حریق (مثلاً NFPA 25 برای سیستم‌های آب‌آتش‌نشانی) - به‌عنوان دسته‌ی کلی مرجع."),
        ("SMRP", "SMRP", "Best Practices", "Society for Maintenance & Reliability Professionals best practices",
            "Reliability / Best Practice", "بهترین‌روش‌های صنعتی نگهداری و قابلیت‌اطمینان (KPI ها و طبقه‌بندی نگهداری) - مرجع مفهومی، نه یک کد اجباری."),
        ("AMPP", "AMPP", "Best Practices", "Association for Materials Protection and Performance practices",
            "Corrosion", "بهترین‌روش‌های مدیریت خوردگی و حفاظت مواد (ادغام NACE/SSPC سابق) - مرجع مفهومی برای گروه Corrosion Management."),

        // طبق «PEMS Phase 4 — RCM/FMEA/FMECA Decision Intelligence» بند ۲۹: دو استاندارد
        // بنیادین RCM - فقط ابرداده (نه متن دارای Copyright).
        ("SAE-JA1011", "SAE", "JA1011", "Evaluation Criteria for Reliability-Centered Maintenance (RCM) Processes",
            "Reliability / RCM", "معیارهای ارزیابی یک فرآیند RCM معتبر - چارچوب مفهومی پایه‌ی ماژول RCM در PEMS (نه یک متن اجباری کپی‌شده)."),
        ("SAE-JA1012", "SAE", "JA1012", "A Guide to the Reliability-Centered Maintenance (RCM) Standard",
            "Reliability / RCM", "راهنمای تفسیری SAE JA1011 - مرجع مفهومی برای واژگان و گردش‌کار RCM (Functional Failure/Failure Mode/Consequence) استفاده‌شده در PEMS."),
    };

    // طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۲:
    // فقط ابرداده‌ی سه استاندارد صراحتاً نام‌برده‌شده در سند (۱۷۳۵۹/۲۰۸۱۶/۱۸۴۳۶) - ISO-14224
    // پیش‌تر در Defaults بالا موجود است. Edition فقط همان سال داخل عنوان رسمی استاندارد است
    // (مثلاً «ISO 17359:2018») - نه یک مقدار حدس‌زده؛ PublishedDate/EffectiveFrom عمداً خالی
    // می‌مانند چون ماه/روز دقیق انتشار از منبع رسمی PEMS تأیید نشده (بند «no fabricated values»).
    private static readonly (string Code, string Org, string Number, string Title, string Edition, string Category, string Description)[] ConditionMonitoringDefaults =
    {
        ("ISO-17359", "ISO", "17359", "Condition monitoring and diagnostics of machines — General guidelines",
            "2018", "Condition Monitoring",
            "چارچوب کلی برنامه‌ریزی یک برنامه‌ی Condition Monitoring (از انتخاب پارامتر تا تصمیم‌گیری) - مرجع مفهومی برای ساختار ماژول CM در PEMS، نه یک متن کپی‌شده."),
        ("ISO-20816-1", "ISO", "20816-1", "Mechanical vibration — Measurement and evaluation of machine vibration — Part 1: General guidelines",
            "2016", "Condition Monitoring / Vibration",
            "چارچوب کلی اندازه‌گیری و ارزیابی ارتعاش ماشین‌آلات - مرجع مفهومی برای EvaluationProfile های نوع Vibration؛ مقادیر آستانه‌ی واقعی فقط با تأیید مهندسی در EvaluationLimit ثبت می‌شوند، نه از این سند."),
        ("ISO-18436-2", "ISO", "18436-2", "Condition monitoring and diagnostics of machines — Requirements for qualification and assessment of personnel — Part 2: Vibration condition monitoring and diagnostics",
            "2014", "Condition Monitoring / Personnel Competence",
            "الزامات صلاحیت پرسنل اندازه‌گیری/تفسیر ارتعاش - حداقل نیاز PEMS همان ثبت مسئول قرائت (Employee) در ConditionReading.RecordedBy است، نه یک پلتفرم گواهی‌نامه‌ی جدید."),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var existingCodes = (await db.StandardReferences.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = Defaults.Where(d => !existingCodes.Contains(d.Code)).ToList();
        foreach (var (code, org, number, title, category, description) in missing)
        {
            db.StandardReferences.Add(new StandardReference
            {
                Code = code,
                Organization = org,
                StandardNumber = number,
                Title = title,
                Category = category,
                Description = description,
                IsSystemDefault = true,
                IsActive = true,
                CreatedDate = DateTime.Now
            });
        }

        var missingCm = ConditionMonitoringDefaults.Where(d => !existingCodes.Contains(d.Code)).ToList();
        foreach (var (code, org, number, title, edition, category, description) in missingCm)
        {
            db.StandardReferences.Add(new StandardReference
            {
                Code = code,
                Organization = org,
                StandardNumber = number,
                Title = title,
                Edition = edition,
                Category = category,
                Description = description,
                Status = "Current",
                IsSystemDefault = true,
                IsActive = true,
                CreatedDate = DateTime.Now
            });
        }

        if (missing.Count == 0 && missingCm.Count == 0) return;

        await db.SaveChangesAsync();
    }
}
