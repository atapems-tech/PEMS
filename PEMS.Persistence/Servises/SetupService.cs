using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS — Quick Setup &amp; Domain Import Architecture Refactoring» بند ۵: هر آیتم یک
/// قاعده‌ی واقعی و قطعی دارد (نه یک درصد فرضی) - همه‌ی کوئری‌ها هدفمند و سبک هستند (AnyAsync،
/// نه بارگذاری کامل جدول) طبق بند ۲۶. لیست ۹ آیتم دقیقاً همان چیزی است که در Mockup بند ۵ سند
/// آمده - "Engineering Structure"/"People &amp; Teams"/"Verification" از لیست ۱۰-موردی مفهومی
/// بند ۴ عمداً به این ۹ مورد قابل‌سنجش تجمیع شدند (طبق دستور صریح سند: "Do NOT blindly implement
/// all ten as fully independent systems - first inspect what already exists").
/// </summary>
public class SetupService : ISetupService
{
    private readonly PEMSDbContext _context;
    private readonly IConfiguration _configuration;

    public SetupService(PEMSDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<SetupStatusDto> GetStatusAsync()
    {
        // طبق بند ۲۶ (Performance): هر Query مستقل و هدفمند است (AnyAsync) - نه Join سنگین.
        var hasProject = await _context.Projects.AnyAsync(x => x.IsActive);
        var hasDepartment = await _context.Departments.AnyAsync(x => x.IsActive);
        var hasDiscipline = await _context.Disciplines.AnyAsync(x => x.IsActive);
        var hasEmployee = await _context.Employees.AnyAsync(x => x.IsActive);
        var hasMaintenanceGroup = await _context.MaintenanceGroups.AnyAsync(x => x.IsActive);
        var hasTeam = await _context.Teams.AnyAsync(x => x.IsActive);
        var hasEquipment = await _context.Equipments.AnyAsync(x => x.IsActive);
        var hasMaterialCategory = await _context.MaterialCategories.AnyAsync(x => x.IsActive);
        var hasMaterialUom = await _context.MaterialUOMs.AnyAsync(x => x.IsActive);
        var hasMaterialItem = await _context.MaterialItems.AnyAsync(x => x.IsActive);
        var hasEngineeringDocument = await _context.EngineeringDocuments.AnyAsync(x => x.IsActive);
        var hasRolePermission = await _context.RolePermissions.AnyAsync();

        var status = new SetupStatusDto();

        // ==================== 1. Organization ====================
        // بدون پیش‌نیاز - پایه‌ای‌ترین سطح (Project/Department مستقیم زیر آن‌ها هیچ‌کدام FK ندارند)
        status.Steps.Add(new SetupStepDto
        {
            Key = "Organization",
            Title = "Organization",
            Description = "پروژه‌ها و دپارتمان‌های سازمان - پایه‌ی همه‌ی داده‌های دیگر.",
            Status = (hasProject && hasDepartment) ? SetupStepStatus.Complete : SetupStepStatus.NotStarted,
            MissingDependencies = BuildMissing(
                (hasProject, "حداقل یک پروژه‌ی فعال"),
                (hasDepartment, "حداقل یک دپارتمان فعال")),
            ActionController = "Projects",
            ActionAction = "Index",
            ActionLabel = "Manage Projects"
        });

        // ==================== 2. Disciplines ====================
        // طبق بازبینی مخزن: هیچ صفحه‌ی CRUD ای برای Discipline در PEMS وجود ندارد - فقط از طریق
        // DisciplineSeeder در استارتاپ Seed می‌شود؛ به همین دلیل لینک این مرحله فقط «نمایش»
        // است (DisciplineController - همون Workspace)، نه «پیکربندی».
        status.Steps.Add(new SetupStepDto
        {
            Key = "Disciplines",
            Title = "Disciplines",
            Description = "دیسیپلین‌های مهندسی/تعمیرات - از طریق Seed داخلی سیستم مقداردهی می‌شوند.",
            Status = hasDiscipline ? SetupStepStatus.Complete : SetupStepStatus.NotStarted,
            MissingDependencies = new List<string>(),
            ActionController = "Discipline",
            ActionAction = "Index",
            ActionLabel = "View Disciplines"
        });

        // ==================== 3. Employees ====================
        status.Steps.Add(new SetupStepDto
        {
            Key = "Employees",
            Title = "Employees",
            Description = "پرسنل سازمان - هر کارمند به یک Department و یک Discipline نیاز دارد.",
            Status = !hasDepartment || !hasDiscipline
                ? SetupStepStatus.Blocked
                : (hasEmployee ? SetupStepStatus.Complete : SetupStepStatus.NotStarted),
            MissingDependencies = BuildMissing(
                (hasDepartment, "Organization (Department)"),
                (hasDiscipline, "Disciplines")),
            ActionController = "Employee",
            ActionAction = "Index",
            ActionLabel = "Manage Employees"
        });

        // ==================== 4. Teams ====================
        status.Steps.Add(new SetupStepDto
        {
            Key = "Teams",
            Title = "Teams",
            Description = "تیم‌های تعمیرونگهداری - هر تیم به یک Maintenance Group (زیر یک Discipline) نیاز دارد.",
            Status = !hasMaintenanceGroup
                ? SetupStepStatus.Blocked
                : (hasTeam ? SetupStepStatus.Complete : SetupStepStatus.NotStarted),
            MissingDependencies = BuildMissing((hasMaintenanceGroup, "Maintenance Group (زیر Maintenance Master Data)")),
            ActionController = hasMaintenanceGroup ? "Team" : "MaintenanceMasterData",
            ActionAction = "Index",
            ActionLabel = hasMaintenanceGroup ? "Manage Teams" : "Configure Maintenance Groups"
        });

        // ==================== 5. Equipment ====================
        status.Steps.Add(new SetupStepDto
        {
            Key = "Equipment",
            Title = "Equipment",
            Description = "رجیستر تجهیزات - هر تجهیز به یک Project و یک Discipline نیاز دارد.",
            Status = !hasProject || !hasDiscipline
                ? SetupStepStatus.Blocked
                : (hasEquipment ? SetupStepStatus.Complete : SetupStepStatus.NotStarted),
            MissingDependencies = BuildMissing(
                (hasProject, "Organization (Project)"),
                (hasDiscipline, "Disciplines")),
            ActionController = "Equipment",
            ActionAction = "Index",
            ActionLabel = "Manage Equipment"
        });

        // ==================== 6. Material Master ====================
        status.Steps.Add(new SetupStepDto
        {
            Key = "MaterialMaster",
            Title = "Material Master",
            Description = "کالاهای مرجع انبار - به Material Category و Unit of Measure نیاز دارد.",
            Status = !hasMaterialCategory || !hasMaterialUom
                ? SetupStepStatus.Blocked
                : (hasMaterialItem ? SetupStepStatus.Complete : SetupStepStatus.NotStarted),
            MissingDependencies = BuildMissing(
                (hasMaterialCategory, "Material Category"),
                (hasMaterialUom, "Unit of Measure")),
            ActionController = (!hasMaterialCategory || !hasMaterialUom) ? "InventoryMasterData" : "MaterialItem",
            ActionAction = "Index",
            ActionLabel = (!hasMaterialCategory || !hasMaterialUom) ? "Configure Inventory Master Data" : "Manage Material Master"
        });

        // ==================== 7. Engineering Documents ====================
        status.Steps.Add(new SetupStepDto
        {
            Key = "EngineeringDocuments",
            Title = "Engineering Documents",
            Description = "رجیستر مدارک مهندسی - به یک Project فعال نیاز دارد.",
            Status = !hasProject
                ? SetupStepStatus.Blocked
                : (hasEngineeringDocument ? SetupStepStatus.Complete : SetupStepStatus.NotStarted),
            MissingDependencies = BuildMissing((hasProject, "Organization (Project)")),
            ActionController = "EngineeringDocuments",
            ActionAction = "Index",
            ActionLabel = "Manage Documents"
        });

        // ==================== 8. Permissions ====================
        // طبق بازبینی Program.cs: PermissionService.SeedDefaultsAsync() در هر استارتاپ اجرا
        // می‌شود - یعنی این مرحله همیشه بعد از اولین اجرای برنامه واقعاً کامل است، نه فرضی.
        status.Steps.Add(new SetupStepDto
        {
            Key = "Permissions",
            Title = "Permissions",
            Description = "پیکربندی پیش‌فرض مجوزها - به‌صورت خودکار در هر استارتاپ سیستم Seed می‌شود.",
            Status = hasRolePermission ? SetupStepStatus.Complete : SetupStepStatus.NotStarted,
            MissingDependencies = new List<string>(),
            ActionController = "PermissionMatrix",
            ActionAction = "Index",
            ActionLabel = "Review Permission Matrix"
        });

        // ==================== 9. AI Configuration ====================
        // طبق بند ۲۶ (Performance): بررسی واقعی Ping کردن Ollama روی هر بار رندر Dashboard
        // ممنوع است (هزینه‌ی شبکه) - فقط "پیکربندی شده" (مقادیر appsettings موجودند) چک می‌شود،
        // نه "الان در دسترس است".
        var ollamaUrl = _configuration["Ollama:Url"];
        var ollamaModel = _configuration["Ollama:Model"];
        var aiConfigured = !string.IsNullOrWhiteSpace(ollamaUrl) && !string.IsNullOrWhiteSpace(ollamaModel);
        status.Steps.Add(new SetupStepDto
        {
            Key = "AIConfiguration",
            Title = "AI Configuration",
            Description = "آدرس و مدل Ollama برای پردازش هوش مصنوعی محلی. (فقط پیکربندی بررسی می‌شود، نه اتصال زنده.)",
            Status = aiConfigured ? SetupStepStatus.Complete : SetupStepStatus.NotStarted,
            MissingDependencies = new List<string>(),
            ActionController = "RoutingRules",
            ActionAction = "Index",
            ActionLabel = "AI Administration"
        });

        return status;
    }

    private static List<string> BuildMissing(params (bool Satisfied, string Label)[] checks) =>
        checks.Where(c => !c.Satisfied).Select(c => c.Label).ToList();
}
