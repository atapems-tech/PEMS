using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.PartCatalogue;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;

namespace PEMS.Persistence.Seed;

/// <summary>
/// Seed «درخواست کاتالوگ قطعه» (Warehouse) - کاملاً Idempotent و فقط-افزودنی:
///  • ردیف‌های مرجع با «کد» کلید می‌خورند؛ ردیف موجود هرگز بازنویسی نمی‌شود (Import دستی مدیر محفوظ می‌ماند).
///  • Excelهای IFS از Assembly (EmbeddedResource) خوانده و با همان کد Import مدیر (حالت insert-only) وارد می‌شوند.
///  • نگاشت‌های Section→CommodityGroup و گام‌های مسیر «نیازمند تأیید» علامت می‌خورند (فایل‌ها آن‌ها را تعریف نمی‌کنند).
/// </summary>
public static class PartCatalogueSeeder
{
    private static readonly (string Code, string Fa, bool Spare, bool Confirm)[] Sections =
    {
        ("HSE", "ایمنی، بهداشت و محیط زیست", true, true),
        ("SERVICES", "خدمات", false, true),
        ("IT", "فناوری اطلاعات", false, true),
        ("SECURITY", "حراست", false, true),
        ("PUBLIC RELATION", "روابط عمومی", false, true),
        ("FINANCIAL", "مالی", false, true),
        ("ENGINEERING", "مهندسی", true, false),
        ("ASSET INTEGRITY", "یکپارچگی دارایی", true, false),
        ("NEW PE PLANT", "واحد جدید PE", true, false),
        ("OUTBOUND WAREHOUSE", "انبار خروجی", true, false),
        ("C2 CRACKER", "کراکر C2", true, false),
        ("LDPE", "LDPE", true, false),
        ("MDPE/HDPE", "MDPE/HDPE", true, false),
        ("OFFSITE", "آفسایت", true, false),
        ("BAGGING", "بسته‌بندی (Bagging)", true, false),
        ("ADMIN", "اداری", false, true),
        ("PROCESS", "فرآیند", true, false),
        ("CENTRAL WORKSHOP", "کارگاه مرکزی", true, false),
        ("CENTRAL LABORATORY", "آزمایشگاه مرکزی", true, false),
        ("LD-PE LABORATORY", "آزمایشگاه LD-PE", true, false),
        ("RESTAURANT", "رستوران", false, true),
        ("GENERAL", "عمومی", false, true),
        ("SUPPORT SERVICES", "خدمات پشتیبانی", false, true),
    };

    private static readonly (string Code, string Name, string Fa)[] Currencies =
    {
        ("IRR", "Iranian Rial", "ریال"), ("USD", "US Dollar", "دلار"), ("EUR", "Euro", "یورو"),
        ("CNY", "Chinese Yuan", "یوان چین"), ("JPY", "Japanese Yen", "ین ژاپن"), ("RUB", "Russian Ruble", "روبل"),
    };

    private static readonly (int Grade, string En, string Fa)[] Grades =
    {
        (1, "Consumable Parts / To be replaced once it is disassembled at every maintenance work", "اقلام مصرفی"),
        (2, "Regular stock for periodical turnaround / To be replaced if any abnormality is identified (e.g. damaged, out of tolerance)", "اقلامی که نیاز به تعویض یا احتمال خراب شدن دارند"),
        (3, "Insurance stock for long term safe operation / Possible to be deteriorated or damaged in long term operation", "اقلام سرمایه‌ای (ذخیره‌ی بیمه‌ای)"),
    };

    private static readonly string[] PlanningMethods = { "A", "B", "C", "D", "E", "F", "G", "K", "M", "N", "P", "T" };

    // کدها عیناً از لیست دستورالعمل. ستاره‌های نمایش‌داده‌شده در تصویر (CAP-CR-RE* ...) خروجی RTL است، نه بخشی از کد.
    private static readonly string[] TypeDesignations =
    {
        "Capital Part", "Critical Part", "Reverse Engineering Part", "Locally Purchased Part", "Surplus Material",
        "CAP-CR", "CAP-CR-RE", "CAP-CR-LOCAL", "CAP-CR-RE-LOCAL", "CR-RE", "CR-LOCAL", "CR-RE-LOCAL", "RE-LOCAL"
    };

    private static readonly (string Key, int Order, string En, string Fa, string Kind, string Source, string Cond, bool Confirm)[] Steps =
    {
        ("DirectManager", 10, "Direct manager approval", "تأیید مدیر مستقیم", PartCatalogueStepKinds.Approval, PartCatalogueApproverSources.DirectManager, PartCatalogueConditions.Always, false),
        ("SuperiorManager", 20, "Superior manager approval", "تأیید مدیر مافوق", PartCatalogueStepKinds.Approval, PartCatalogueApproverSources.SuperiorManager, PartCatalogueConditions.Always, true),
        ("MaintenanceSupervisor", 30, "Maintenance supervisor review (Critical/Capital)", "بررسی سرپرست تعمیرات (Critical/Capital)", PartCatalogueStepKinds.Review, PartCatalogueApproverSources.AssignedPool, PartCatalogueConditions.CriticalOrCapital, true),
        ("SparePartsSpecialist", 40, "Spare parts specialist review", "بررسی کارشناس قطعات", PartCatalogueStepKinds.Review, PartCatalogueApproverSources.AssignedPool, PartCatalogueConditions.SectionNeedsSpareReview, true),
        ("SourcingUnit", 50, "Sourcing unit approval (new manufacturer)", "تأیید واحد سورسینگ (Manufacturer جدید)", PartCatalogueStepKinds.Approval, PartCatalogueApproverSources.AssignedPool, PartCatalogueConditions.NewManufacturer, false),
        ("CommodityGroupReview", 60, "Product category (commodity group) review", "بررسی گروه دسته‌بندی محصول", PartCatalogueStepKinds.Review, PartCatalogueApproverSources.AssignedPool, PartCatalogueConditions.Always, false),
        ("PartCataloguer", 70, "Part cataloguer: IFS registration and part number", "Part Cataloguer: ثبت در IFS و درج شماره‌ی قطعه", PartCatalogueStepKinds.Registration, PartCatalogueApproverSources.AssignedPool, PartCatalogueConditions.Always, true),
        ("InventoryControl", 80, "Inventory control team review (Planning Method ≠ A)", "بررسی تیم کنترل موجودی (Planning Method ≠ A)", PartCatalogueStepKinds.Review, PartCatalogueApproverSources.AssignedPool, PartCatalogueConditions.PlanningMethodNotA, false),
    };

    // نگاشت صریحِ ذکرشده در درخواست + Section=HSE از تصویر دستورالعمل (فایل Excel آن‌ها را ندارد)
    private static readonly (string Section, string[] Groups)[] SectionGroups =
    {
        ("HSE", new[] { "10850", "10870", "10880", "10890" }),
        ("C2 CRACKER", new[] { "10400" }),
        ("LDPE", new[] { "10300" }),
        ("CENTRAL WORKSHOP", new[] { "10560" }),
        ("CENTRAL LABORATORY", new[] { "10701" }),
        ("LD-PE LABORATORY", new[] { "10702" }),
    };

    private static Stream? Embedded(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream($"PEMS.Seed.PartCatalogue.{name}");

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();
        await SeedAsync(db);
    }

    public static async Task SeedAsync(PEMSDbContext db, bool importExcel = true)
    {
        // ---- جدول‌های کوچک: فقط ردیف‌های گم‌شده
        var sectionCodes = (await db.PartCatalogueSections.Select(s => s.Code).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var order = 10;
        foreach (var (code, fa, spare, confirm) in Sections)
        {
            if (!sectionCodes.Contains(code))
                db.PartCatalogueSections.Add(new PartCatalogueSection { Code = code, NameFa = fa, SortOrder = order, RequiresSparePartsReview = spare, RoutingNeedsConfirmation = confirm });
            order += 10;
        }

        var curCodes = (await db.PartCatalogueCurrencies.Select(c => c.Code).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var co = 1;
        foreach (var (code, name, fa) in Currencies)
        {
            if (!curCodes.Contains(code)) db.PartCatalogueCurrencies.Add(new PartCatalogueCurrency { Code = code, Name = name, NameFa = fa, SortOrder = co });
            co++;
        }

        var gradeNos = (await db.PartCatalogueStockGrades.Select(g => g.Grade).ToListAsync()).ToHashSet();
        foreach (var (g, en, fa) in Grades)
            if (!gradeNos.Contains(g)) db.PartCatalogueStockGrades.Add(new PartCatalogueStockGrade { Grade = g, Description = en, DescriptionFa = fa });

        var pmCodes = (await db.PartCataloguePlanningMethods.Select(p => p.Code).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var c in PlanningMethods)
            if (!pmCodes.Contains(c))
                db.PartCataloguePlanningMethods.Add(new PartCataloguePlanningMethod
                {
                    Code = c,
                    Description = c == "A" ? "Minimum Stock and Max Consumption Rate are forced to 0"
                        : c == "B" ? "User-entered Minimum Stock and Max Consumption Rate are used" : null
                });

        var tdCodes = (await db.PartCatalogueTypeDesignations.Select(t => t.Code).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var to = 1;
        foreach (var c in TypeDesignations)
        {
            if (!tdCodes.Contains(c))
            {
                var (cr, cap) = PartCatalogueRules.DeriveTypeFlags(c);
                db.PartCatalogueTypeDesignations.Add(new PartCatalogueTypeDesignation { Code = c, IsCritical = cr, IsCapital = cap, SortOrder = to });
            }
            to++;
        }

        // سازنده‌ی LOCAL (نیاز قاعده‌ی NO_PART_NR)
        if (!await db.PartCatalogueManufacturers.AnyAsync(m => m.Name == PartCatalogueRules.LocalManufacturerName))
            db.PartCatalogueManufacturers.Add(new PartCatalogueManufacturer
            { Name = PartCatalogueRules.LocalManufacturerName, NormalizedName = PartCatalogueRules.LocalManufacturerName, IsLocal = true });

        var stepKeys = (await db.PartCatalogueWorkflowSteps.Select(s => s.StepKey).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, ord, en, fa, kind, src, cond, confirm) in Steps)
            if (!stepKeys.Contains(key))
                db.PartCatalogueWorkflowSteps.Add(new PartCatalogueWorkflowStep
                { StepKey = key, StepOrder = ord, NameEn = en, NameFa = fa, Kind = kind, ApproverSource = src, ConditionKey = cond, NeedsConfirmation = confirm });

        await db.SaveChangesAsync();

        // ---- فایل‌های IFS (insert-only)
        var admin = new PartCatalogueAdminService(db);
        if (importExcel && !await db.PartCatalogueTechnicalClasses.AnyAsync())
            using (var s = Embedded("TechnicalClasses.xlsx")) { if (s != null) await admin.ImportTechnicalClassesAsync(s, updateExisting: false); }
        if (importExcel && !await db.PartCatalogueCommodityGroups.AnyAsync())
            using (var s = Embedded("CommodityGroups.xlsx")) { if (s != null) await admin.ImportCommodityGroupsAsync(s, updateExisting: false); }
        if (importExcel && !await db.PartCatalogueProductFamilies.AnyAsync())
            using (var s = Embedded("ProductFamilies.xlsx")) { if (s != null) await admin.ImportProductFamiliesAsync(s, updateExisting: false); }

        // کدهایی که فقط در تصویر دستورالعمل (Section = HSE) دیده شده‌اند و در فایل Excel نیستند
        var groupCodes = (await db.PartCatalogueCommodityGroups.Select(g => g.Code).ToListAsync()).ToHashSet();
        foreach (var (code, desc) in new[] { ("10870", "Firefighting"), ("10880", "Health"), ("10890", "Environment") })
            if (!groupCodes.Contains(code))
                db.PartCatalogueCommodityGroups.Add(new PartCatalogueCommodityGroup { Code = code, Description = desc, NeedsConfirmation = true });
        await db.SaveChangesAsync();

        // ---- نگاشت Section → Group1 (فقط اجرای اول؛ بعد از آن مدیر مالک است)
        if (!await db.PartCatalogueSectionCommodityGroups.AnyAsync())
        {
            var sections = await db.PartCatalogueSections.ToDictionaryAsync(s => s.Code, s => s.SectionId, StringComparer.OrdinalIgnoreCase);
            var groups = await db.PartCatalogueCommodityGroups.ToDictionaryAsync(g => g.Code, g => g.CommodityGroupId);
            foreach (var (section, codes) in SectionGroups)
                foreach (var gc in codes)
                    if (sections.TryGetValue(section, out var sid) && groups.TryGetValue(gc, out var gid))
                        db.PartCatalogueSectionCommodityGroups.Add(new PartCatalogueSectionCommodityGroup { SectionId = sid, CommodityGroupId = gid, NeedsConfirmation = true });
            await db.SaveChangesAsync();
        }
    }
}
