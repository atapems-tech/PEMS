namespace PEMS.Persistence.Identity;

/// <summary>
/// نام‌های ثابت نقش‌های سیستم؛ برای استفاده در [Authorize(Roles = ...)]، Seed کردن نقش‌ها،
/// و فیلتر کردن منوها بر اساس نقش، به جای رشته‌ی خام در جاهای مختلف کد استفاده شود.
/// </summary>
public static class SystemRoles
{
    public const string SystemAdmin = "SystemAdmin";
    public const string EngineeringManager = "EngineeringManager";
    public const string EngineeringSupervisor = "EngineeringSupervisor";
    public const string DisciplineEngineer = "DisciplineEngineer";
    public const string DocumentController = "DocumentController";
    public const string MaintenanceEngineer = "MaintenanceEngineer";
    public const string ContractManager = "ContractManager";
    public const string QAManager = "QAManager";
    public const string Viewer = "Viewer";

    // طبق ممیزی Package Procurement Contract Finalization (§F): سه نقش تازه، صرفاً برای پوشش‌دادن
    // رشته‌های نام‌برده‌شده‌ی صریح در بازبینی چند-رشته‌ای قرارداد (Legal/Finance/HSE) که پیش از این
    // هیچ نقشی برایشان وجود نداشت. هیچ ساختار سازمانی دیگری اختراع نشده.
    public const string Legal = "Legal";
    public const string Finance = "Finance";
    public const string HSE = "HSE";

    public static readonly string[] All =
    {
        SystemAdmin,
        EngineeringManager,
        EngineeringSupervisor,
        DisciplineEngineer,
        DocumentController,
        MaintenanceEngineer,
        ContractManager,
        QAManager,
        Viewer,
        Legal,
        Finance,
        HSE
    };

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        [SystemAdmin] = "System Admin",
        [EngineeringManager] = "Engineering Manager",
        [EngineeringSupervisor] = "Engineering Supervisor",
        [DisciplineEngineer] = "Discipline Engineer",
        [DocumentController] = "Document Controller",
        [MaintenanceEngineer] = "Maintenance Engineer",
        [ContractManager] = "Contract Manager",
        [QAManager] = "QA Manager",
        [Viewer] = "Viewer",
        [Legal] = "Legal",
        [Finance] = "Finance",
        [HSE] = "HSE"
    };
}
