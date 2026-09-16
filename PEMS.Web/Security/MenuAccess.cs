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

    // طبق «PEMS Operations CMMS Boundary Correction»: مستقل از CmmsIntegration بالا - دقیقاً
    // همون نقش‌هایی که PermissionService.SeedDefaultsAsync برای ماژول "Production" مجوز
    // گرفتند (MaintenanceEngineer/QAManager/EngineeringManager/Viewer)، تا هیچ نقشی لینک را
    // ببیند که پشتش Forbid بگیرد یا برعکس.
    public static readonly string[] Operations =
    {
        SystemRoles.SystemAdmin,
        SystemRoles.MaintenanceEngineer,
        SystemRoles.QAManager,
        SystemRoles.EngineeringManager,
        SystemRoles.Viewer
    };

    public static readonly string[] Administration =
    {
        SystemRoles.SystemAdmin
    };

    // طبق «PEMS — Quick Setup & Domain Import Architecture Refactoring» بند ۷/۲۸: Import ها
    // قابلیت دامنه‌ای هستند (Import is a domain capability, not an Administration capability) -
    // MDR/VDR/Equipment/MRS Import هرکدام حالا داخل بخش دامنه‌ی خودشان دیده می‌شوند (Engineering/
    // CMMS/Inventory)، نه زیر Administration. این آرایه فقط تعیین می‌کند چه نقش‌هایی اجازه دارند
    // همان لینک را - در هرکجا که رندر شود - ببینند؛ باید دقیقاً با نقش‌هایی که در
    // PermissionService.SeedDefaultsAsync برای Commit/Approve این چهار Pipeline مجوز گرفتند
    // (DocumentController، EngineeringManager، MaintenanceEngineer، Viewer) یکی بماند - وگرنه
    // یا لینک مرده می‌سازد (نقش می‌بیند ولی 403 می‌گیرد) یا مجوز Backend را بی‌راه می‌کند
    // (نقش می‌تواند از URL مستقیم برود ولی لینک را نمی‌بیند).
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
