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


    public static bool CanSee(
        System.Security.Claims.ClaimsPrincipal user,
        string[] allowedRoles)
    {
        return allowedRoles.Any(user.IsInRole);
    }
}
