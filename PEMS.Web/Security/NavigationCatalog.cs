namespace PEMS.Web.Security;

/// <summary>
/// طبق سند «PEMS Phase 6 — User Module & Navigation Visibility Architecture»: تنها کاتالوگ
/// کلیدهای پایدار سایدبار - قبل از این فاز هیچ آیتمی شناسه‌ی پایدار نداشت (فقط Controller/Action
/// خام در Razor). این کلاس چیزی «تصمیم» نمی‌گیرد - فقط اسم می‌گذارد؛ تصمیم واقعی در
/// NavigationVisibilityResolver است.
///
/// طبق تصمیم دامنه‌ی فاز ۶ (Scope Decision - چون ممیزی صریحاً درخواست «Drill-down سطح آیتم فقط
/// برای یک ماژول نماینده‌ی واقعی» کرده بود، نه بازنویسیِ ~۱۰۰ آیتم سایدبار): Drill-down سطح آیتم
/// فقط برای CmmsIntegration پیاده شده - چون خودِ مثال‌های سند (Work Order/Maintenance Plan/Work
/// Request) زیرمجموعه‌ی همین ماژول‌اند. بقیه‌ی ماژول‌ها فقط سطح-ماژول دارند (دقیقاً هم‌سطح
/// MenuAccess امروز) + قابلیت Override دستی مدیر.
/// </summary>
public static class NavigationCatalog
{
    public static readonly string[] ModuleKeys =
    {
        "EngineeringManagement",
        "AIIntelligence",
        "CorrespondenceDocControl",
        "QualityControl",
        "ContractManagement",
        "ProjectControls",
        "CmmsIntegration",
        "Inventory",
        "Operations",
        "Administration",
        "QuickSetup",
        "ImportCenter"
    };

    /// <summary>
    /// نگاشت هر آیتم CmmsIntegration به نام واقعی Resource همان کنترلر (همانی که در
    /// HasPermissionAsync/HasActionAsync آن کنترلر واقعاً استفاده می‌شود - عیناً از سورس، نه
    /// اختراعی). مقدار null یعنی آن کنترلر امروز هیچ دروازه‌ی مجوز مستقلی ندارد - برای این‌ها
    /// پیش‌فرض محاسبه نمی‌شود (فقط قابل Override دستی)، تا چیزی که سیگنال واقعی ندارد به‌غلط
    /// «قابل استنتاج» نشان داده نشود.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string?> CmmsIntegrationItemResourceMap =
        new Dictionary<string, string?>
        {
            // طبق بررسی زنده‌ی دیتابیس Dev (نه فقط PermissionService.SeedDefaultsAsync که فقط ۵
            // ماژول را سید می‌کند): برخلاف بقیه‌ی آیتم‌های این نگاشت، Grant واقعی "Equipment.View"
            // امروز فقط به MaintenanceEngineer داده شده - نه EngineeringManager/EngineeringSupervisor
            // که هر دو همین الان (از طریق دروازه‌ی درشت MenuAccess.CmmsIntegration) این آیتم را
            // می‌بینند. اگر این‌جا "Equipment" نگاشته می‌شد، این دو نقش بلافاصله بعد از فاز ۶ آن را
            // از دست می‌دادند - یک تراجعِ واقعی که تصحیحش (افزودن Grant) یک تصمیم Authorization
            // است، نه Visibility، و بیرون از حوزه‌ی این فاز. پس عمداً null می‌ماند - دقیقاً هم‌رفتار
            // Instrument/Asset/SparePart (بدون سیگنال قابل‌اتکا = همیشه نمایان، رفتار قبل از فاز ۶).
            ["Equipment"] = null,
            ["Instrument"] = null,
            ["Asset"] = null,
            ["MaintenanceStrategy"] = "MaintenanceStrategy",
            ["MaintenancePlan"] = "MaintenancePlan",
            ["MaintenanceCalendar"] = "WorkOrder",
            ["JobPlan"] = "JobPlan",
            ["WorkRequest"] = "WorkRequest",
            ["WorkOrder"] = "WorkOrder",
            ["SparePart"] = null,
            ["Team"] = "Team",
            ["Crew"] = "Crew",
            ["ShiftHandover"] = "ShiftHandover",
            ["MaintenanceMasterData"] = "MaintenanceMasterData",
            ["EquipmentClassification"] = "MaintenanceMasterData",
            ["RcmFmea"] = "RCMFMEA",
            ["EquipmentCriticality"] = "EquipmentCriticality",
            ["CriticalityAssessment"] = "CriticalityAssessment",
            ["FunctionalLocation"] = "FunctionalLocation",
            ["Barcode"] = "Barcode",
            ["Reliability"] = "Reliability",
            ["Inspection"] = "Inspection",
            ["Kpi"] = "Reliability",
            ["Permit"] = "Permit",
            ["CmmsReadiness"] = "MaintenanceMasterData",
            ["CmmsOperations"] = "WorkOrder",
        };

    /// <summary>
    /// طبق «PEMS Phase 6 — Hierarchical Role/Discipline Visibility Correction» بند ۶: کلید نمایش
    /// هر ماژول، عیناً همان کلید Localizer که _SidebarMenu.cshtml همین امروز برای تیتر همان بخش
    /// استفاده می‌کند - نه رشته‌ی تازه‌اختراع‌شده. UI مدیریت باید دقیقاً همان سلسله‌مراتب/برچسبی را
    /// نشان دهد که کاربر واقعی در سایدبار می‌بیند.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ModuleDisplayLocalizationKey =
        new Dictionary<string, string>
        {
            ["EngineeringManagement"] = "EngineeringManagement",
            ["AIIntelligence"] = "AI & Knowledge",
            ["CorrespondenceDocControl"] = "CorrespondenceDocControl",
            ["QualityControl"] = "QualityControl",
            ["ContractManagement"] = "ContractManagement",
            ["ProjectControls"] = "ProjectControls",
            ["CmmsIntegration"] = "CmmsIntegration",
            ["Inventory"] = "Warehouse",
            ["Operations"] = "Operations & Production",
            ["Administration"] = "Administration",
            ["QuickSetup"] = "Quick Setup",
            ["ImportCenter"] = "IC_Title",
        };

    /// <summary>
    /// عیناً همان کلید Localizer که هر لینک CmmsIntegration در _SidebarMenu.cshtml امروز نمایش
    /// می‌دهد - دو Route ("Backlog" و "Dashboard") زیر یک ItemKey مشترک ("CmmsOperations") قرار
    /// دارند؛ برچسب نماینده‌ی همان یکی که در زیرگروه Maintenance Planning ظاهر می‌شود انتخاب شده.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> CmmsItemDisplayLocalizationKey =
        new Dictionary<string, string>
        {
            ["Equipment"] = "Equipment",
            ["Instrument"] = "Instrument Passport",
            ["Asset"] = "Assets",
            ["MaintenanceStrategy"] = "Maintenance Strategy",
            ["MaintenancePlan"] = "Maintenance Plan",
            ["MaintenanceCalendar"] = "Maintenance Calendar",
            ["JobPlan"] = "Job Plans",
            ["WorkRequest"] = "Work Requests",
            ["WorkOrder"] = "Work Order",
            ["SparePart"] = "Spare Parts",
            ["Team"] = "Teams",
            ["Crew"] = "Crews",
            ["ShiftHandover"] = "Shift Handovers",
            ["MaintenanceMasterData"] = "Maintenance Master Data",
            ["EquipmentClassification"] = "Equipment Classification",
            ["RcmFmea"] = "RCM / FMEA",
            ["EquipmentCriticality"] = "Criticality Analysis",
            ["CriticalityAssessment"] = "Criticality Assessment",
            ["FunctionalLocation"] = "Functional Locations",
            ["Barcode"] = "Barcode",
            ["Reliability"] = "Reliability",
            ["Inspection"] = "Inspections",
            ["Kpi"] = "KPI Dashboard",
            ["Permit"] = "Permits",
            ["CmmsReadiness"] = "CMMS Readiness",
            ["CmmsOperations"] = "Maintenance Backlog",
        };

    /// <summary>
    /// طبق سلسله‌مراتب واقعی موجود در _SidebarMenu.cshtml (زیرتیتر «Maintenance Planning» زیر
    /// CmmsIntegration) - این‌جا عیناً از همان تودرتویی موجود خوانده شده، نه یک سلسله‌مراتب تازه‌
    /// ساخته‌شده از روی Permission (طبق بند ۶ سند اصلاحی: «Permission Catalog باید هرگز منبع
    /// سلسله‌مراتب ناوبری نباشد»).
    /// </summary>
    public static readonly string[] CmmsMaintenancePlanningSubgroupItemKeys =
    {
        "MaintenanceStrategy",
        "MaintenancePlan",
        "CmmsOperations",
        "MaintenanceCalendar",
    };

    /// <summary>ترتیب رندر واقعی آیتم‌های CmmsIntegration در _SidebarMenu.cshtml (Dictionary ترتیب را تضمین نمی‌کند).</summary>
    public static readonly string[] CmmsIntegrationItemOrder =
    {
        "Equipment", "Instrument", "Asset",
        "MaintenanceStrategy", "MaintenancePlan", "CmmsOperations", "MaintenanceCalendar",
        "JobPlan", "WorkRequest", "WorkOrder", "SparePart", "Team", "Crew", "ShiftHandover",
        "MaintenanceMasterData", "EquipmentClassification", "RcmFmea", "EquipmentCriticality",
        "CriticalityAssessment", "Reliability", "Inspection", "Kpi", "Permit",
        "FunctionalLocation", "Barcode", "CmmsReadiness",
    };
}
