using PEMS.Persistence.Identity;

namespace PEMS.Web.Security;

/// <summary>
/// مشخص می‌کند هر بخش از منوی اصلی سایت برای کدام نقش‌ها قابل مشاهده است.
/// از این کلاس در Views/Shared/_SidebarMenu.cshtml استفاده می‌شود.
/// </summary>
public static class MenuAccess
{
    public static readonly string[] EngineeringManagement =
    {
        SystemRoles.SystemAdmin,
        SystemRoles.EngineeringManager,
        SystemRoles.EngineeringSupervisor,
        SystemRoles.DisciplineEngineer,
        SystemRoles.DocumentController,
        SystemRoles.Viewer
    };

    public static readonly string[] AIIntelligence =
    {
        SystemRoles.SystemAdmin,
        SystemRoles.EngineeringManager,
        SystemRoles.DisciplineEngineer,
        SystemRoles.DocumentController,
        SystemRoles.ContractManager
    };

    public static readonly string[] ContractManagement =
    {
        SystemRoles.SystemAdmin,
        SystemRoles.EngineeringManager,
        SystemRoles.ContractManager
    };

    // نامه‌نگاری، ارجاعات، DCC و TQ/TSR/RFI - قبلا زیرمجموعه‌ی نادرست ContractManagement بودند
    public static readonly string[] CorrespondenceDocControl =
    {
        SystemRoles.SystemAdmin,
        SystemRoles.EngineeringManager,
        SystemRoles.EngineeringSupervisor,
        SystemRoles.DocumentController,
        SystemRoles.ContractManager
    };

    // QC/QA و MOC - قبلا زیرمجموعه‌ی نادرست ContractManagement بودند؛ نقش QAManager قبلا در هیچ بخشی دیده نمی‌شد
    public static readonly string[] QualityControl =
    {
        SystemRoles.SystemAdmin,
        SystemRoles.EngineeringManager,
        SystemRoles.EngineeringSupervisor,
        SystemRoles.QAManager,
        SystemRoles.ContractManager
    };

    // Tasks/Tags/Reports/Meetings - ابزارهای عمومی کنترل پروژه، قبلا زیرمجموعه‌ی نادرست ContractManagement بودند
    public static readonly string[] ProjectControls =
    {
        SystemRoles.SystemAdmin,
        SystemRoles.EngineeringManager,
        SystemRoles.ContractManager
    };

    public static readonly string[] CmmsIntegration =
    {
        SystemRoles.SystemAdmin,
        SystemRoles.EngineeringManager,
        SystemRoles.EngineeringSupervisor,
        SystemRoles.MaintenanceEngineer
    };

    public static readonly string[] Administration =
    {
        SystemRoles.SystemAdmin
    };

    // طبق «PEMS — Global Engineering Management UI/UX, Navigation & RBAC Hardening» بند ۹-۱۲:
    // MDR/VDR/Equipment/MRS Import اقدامات ریسک‌بالای «تایید نهایی داده» هستند و باید از منوهای
    // روزمره‌ی Engineering/CMMS/Inventory جدا و زیر «Administration > Import Center» دیده شوند؛
    // اما همان نقش‌هایی که در PermissionService.SeedDefaultsAsync برای Commit/Approve این سه
    // Pipeline مجوز گرفتند (DocumentController، EngineeringManager، MaintenanceEngineer) باید
    // همچنان لینک این صفحات را در سایدبار ببینند - در غیر این صورت مجوز Backend دارند اما هیچ
    // راهی برای رسیدن به صفحه از منو ندارند (نقض بند ۴۴: «سایدبار را بیش‌از‌حد ساده نکن»).
    public static readonly string[] ImportCenter =
    {
        SystemRoles.SystemAdmin,
        SystemRoles.EngineeringManager,
        SystemRoles.DocumentController,
        SystemRoles.MaintenanceEngineer,
        SystemRoles.Viewer
    };

    // OCR Processing/AI Extraction/Table Extraction/Relationship Engine/AI Intake Queue ابزارهای
    // داخلی خطِ پردازش AI هستند (نه قابلیت کسب‌وکاری کاربر عادی) و طبق ممیزی هیچ‌کدام بررسی
    // مجوز مستقلی در Controller ندارند؛ محدودشان می‌کنیم به همون سطحی که AI Routing Rules از
    // قبل دارد (فقط SystemAdmin) تا هم با هدف بند ۱۴ هم‌خوان باشد هم بی‌جهت دسترسی کسی گسترش نیابد.
    public static readonly string[] AIAdministration =
    {
        SystemRoles.SystemAdmin
    };


    public static bool CanSee(
        System.Security.Claims.ClaimsPrincipal user,
        string[] allowedRoles)
    {
        return allowedRoles.Any(user.IsInRole);
    }
}
