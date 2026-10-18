using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق CMMS-10a ADR-02: RolePermission موجود فقط View/Create/Edit/Delete/Approve را پوشش
/// می‌دهد و برای Permit (با ~۱۴ عمل عملیاتی مثل Issue/Suspend/Resume/Revoke) کافی نیست.
/// این سرویس یک مکانیزم Role/Module/Action عمومی و موازی است - RolePermission برای ۱۲ ماژول
/// موجود دست‌نخورده می‌ماند؛ این‌جا فقط Permit را Seed می‌کند (طبق بند ۱۴: نقطه‌ی اتصال، نه
/// برنامه‌ی کامل مجوزدهی که در CMMS-10b تکمیل می‌شود).
/// </summary>
public class RoleModuleActionService : IRoleModuleActionService
{
    private delegate void AddRoleActions(string role, params string[] actions);

    private readonly PEMSDbContext _context;
    private readonly PEMS.Application.Interfaces.IAccessControlService _accessControlService;

    public RoleModuleActionService(PEMSDbContext context, PEMS.Application.Interfaces.IAccessControlService accessControlService)
    {
        _context = context;
        _accessControlService = accessControlService;
    }

    public async Task<bool> HasActionAsync(IEnumerable<string> userRoles, string module, string action)
    {
        // طبق سند RBAC-ABAC Authorization Architecture (فاز ۱): تصمیم واقعی اکنون از
        // AccessControlService می‌آید (که خودش عیناً همین استثنای Break-Glass زیر را دوباره
        // اعمال می‌کند - طبق سند PEMS-SEC-PERMIT-AUTH-001 بند ۱۲). رفتار باید یکسان بماند -
        // اثبات‌شده با AccessControlParityTests.
        var decision = await _accessControlService.AuthorizeForRolesAsync(userRoles, module, action);
        return decision.Allowed;
    }

    public async Task SeedDefaultsAsync()
    {
        // طبق CMMS-10c: هر ماژول باید گارد مستقل خودش را داشته باشد - وگرنه با یک گارد کلی
        // «اگه Permit سید شده کلاً رد شو»، ماژول جدید (LOTO) هیچ‌وقت روی دیتابیس‌هایی که از
        // قبل Permit را سید کرده‌اند اجرا نمی‌شود. دقیقاً همون درسی که در PermissionService
        // (بند WorkOrder) قبلاً اعمال شده.
        await SeedModuleAsync("Permit", Add =>
        {
            // طبق نقش‌های سند CMMS-10a §3 که هنوز در SystemRoles تعریف نشده‌اند (Work
            // Supervisor, Senior Shift, HSE/Permit Authority و...)، از نزدیک‌ترین نقش‌های
            // موجود در پروژه استفاده می‌شود - تعریف نقش‌های اختصاصی Permit موضوع CMMS-10b است.
            //
            // طبق CMMS-10b §16: MaintenanceEngineer نقش تایید مرحله‌ی MaintenanceApproval در
            // زنجیره‌ی PermitApprovalWorkflowSeeder است - باید Approve/Reject لایه‌ی درشت را هم
            // داشته باشد، وگرنه هیچ‌وقت به لایه‌ی ریزِ موتور گردش‌کار نمی‌رسد (باگ کشف‌شده حین
            // تست زنده).
            Add(SystemRoles.MaintenanceEngineer, "View", "Create", "Edit", "Submit", "Approve", "Reject", "Cancel");
            Add(SystemRoles.EngineeringSupervisor, "View", "Create", "Edit", "Submit", "Approve", "Reject", "Issue", "Suspend", "Resume", "Cancel");
            Add(SystemRoles.EngineeringManager, "View", "Create", "Edit", "Submit", "Approve", "Reject", "Issue", "Suspend", "Resume", "Close", "Cancel", "Revoke");
            Add(SystemRoles.QAManager, "View", "Approve", "Reject");
            Add(SystemRoles.Viewer, "View");
        });

        // طبق CMMS-10c: نقش اختصاصی "Isolation Authority" در SystemRoles وجود ندارد - از
        // نزدیک‌ترین نقش‌های موجود استفاده شده (MaintenanceEngineer برای اجرای فیزیکی
        // ایزوله‌سازی، EngineeringSupervisor/EngineeringManager برای Verify مستقل).
        await SeedModuleAsync("LOTO", Add =>
        {
            Add(SystemRoles.MaintenanceEngineer, "View", "Create", "Isolate", "Deisolate", "Cancel");
            Add(SystemRoles.EngineeringSupervisor, "View", "Create", "Isolate", "Verify", "Deisolate", "Cancel");
            Add(SystemRoles.EngineeringManager, "View", "Create", "Isolate", "Verify", "Deisolate", "Cancel");
            Add(SystemRoles.QAManager, "View");
            Add(SystemRoles.Viewer, "View");
        });

        // طبق CMMS-10d: بدون نقش اختصاصی "Gas Tester"/"HSE Officer" - از نزدیک‌ترین نقش‌های
        // موجود استفاده شده، همون انضباط CMMS-10c. GasTest اتمی است (بدون Draft) - فقط
        // View/Create/Invalidate توجیه دارند (بند ۱۱ پلن).
        await SeedModuleAsync("GasTest", Add =>
        {
            Add(SystemRoles.MaintenanceEngineer, "View", "Create");
            Add(SystemRoles.EngineeringSupervisor, "View", "Create", "Invalidate");
            Add(SystemRoles.EngineeringManager, "View", "Create", "Invalidate");
            Add(SystemRoles.QAManager, "View");
            Add(SystemRoles.Viewer, "View");
        });

        // طبق CMMS-10e: نقش اختصاصی "HSE Officer"/"Operations Coordinator" وجود ندارد - از
        // نزدیک‌ترین نقش‌های موجود استفاده شده (همون انضباط LOTO/GasTest). Confirm/Resolve/
        // AcceptRisk نیازمند نقش‌های ارشد (همون الگوی Verify در LOTO و Invalidate در GasTest).
        await SeedModuleAsync("Simops", Add =>
        {
            Add(SystemRoles.MaintenanceEngineer, "View", "Create");
            Add(SystemRoles.EngineeringSupervisor, "View", "Create", "Confirm", "Resolve", "AcceptRisk");
            Add(SystemRoles.EngineeringManager, "View", "Create", "Confirm", "Resolve", "AcceptRisk");
            Add(SystemRoles.QAManager, "View");
            Add(SystemRoles.Viewer, "View");
        });

        // طبق CMMS-10e: گیت درشت این ماژول عمداً برای همه‌ی نقش‌های عملیاتی روزمره باز است -
        // محدودیت واقعی از طریق تایید هویت دقیق کارمند (Outgoing/Incoming) در ShiftHandoverService
        // اعمال می‌شود، نه از طریق نقش (همون الگوی گیت درشت+ریز GasTest).
        await SeedModuleAsync("ShiftHandover", Add =>
        {
            Add(SystemRoles.MaintenanceEngineer, "View", "Create", "Prepare", "Acknowledge", "Complete");
            Add(SystemRoles.EngineeringSupervisor, "View", "Create", "Prepare", "Acknowledge", "Complete");
            Add(SystemRoles.EngineeringManager, "View", "Create", "Prepare", "Acknowledge", "Complete");
            Add(SystemRoles.QAManager, "View");
            Add(SystemRoles.Viewer, "View");
        });

        // طبق CMMS-10f بند ۶/۱۶ پلن: StartWork دقیقاً همون نقش‌های Permit/Issue را دارد.
        // EmergencyOverride عمداً به هیچ نقشی داده نمی‌شود - طبق تصمیم صریح کاربر در تایید پلن
        // (بند ۱۰ دستورالعمل: "Do not allow ordinary Permit users... If undefined: STOP... Do
        // not guess") - فقط SystemAdmin (Bypass عمومی موجود در HasActionAsync) می‌تواند تا وقتی
        // که نقش واقعی مشخص شود.
        await SeedModuleAsync("ExecutionGate", Add =>
        {
            Add(SystemRoles.MaintenanceEngineer, "View");
            Add(SystemRoles.EngineeringSupervisor, "View", "StartWork");
            Add(SystemRoles.EngineeringManager, "View", "StartWork");
            Add(SystemRoles.QAManager, "View");
            Add(SystemRoles.Viewer, "View");
        });

        // StoreRequisition (Indent داخلی): مجوزهای ریزدانه‌ی چرخه‌ی عمر (Submit/Approve/Reject/SetApprovedQuantity/Reserve/Issue/Close/CloseShort/Cancel)
        // که سرویس چرخه‌ی عمر سمت سرور اعمال می‌کند. پیش‌فرض‌ها از ماتریس قدیمی RolePermission همین ماژول مشتق می‌شود تا رفتار فعلی
        // حفظ شود (CanCreate ⇒ Create+Submit؛ CanApprove ⇒ بقیه‌ی عمل‌های تصمیم‌گیری) + دو نقش انبار. یک‌بار اجرا (Idempotent)؛ بعد از آن
        // مدیر از ماتریس مجوز-عمل تغییر می‌دهد.
        if (!await _context.RoleModuleActions.AnyAsync(x => x.Module == "StoreRequisition"))
        {
            var srRows = new List<RoleModuleAction>();
            void Grant(string role, params string[] actions)
            {
                foreach (var a in actions)
                    if (!srRows.Any(r => r.RoleName == role && r.Action == a))
                        srRows.Add(new RoleModuleAction { RoleName = role, Module = "StoreRequisition", Action = a, CreatedDate = DateTime.Now });
            }
            var decide = new[] { "Approve", "Reject", "SetApprovedQuantity", "Reserve", "Issue", "Close", "CloseShort", "Cancel" };

            foreach (var rp in await _context.RolePermissions.AsNoTracking().Where(x => x.ModuleName == "StoreRequisition").ToListAsync())
            {
                if (rp.CanView) Grant(rp.RoleName, "View");
                if (rp.CanCreate) Grant(rp.RoleName, "Create", "Submit");
                if (rp.CanApprove) Grant(rp.RoleName, decide);
            }

            Grant(RoleCatalog.WarehouseManager, "View", "Create", "Submit");
            Grant(RoleCatalog.WarehouseManager, decide);
            Grant(RoleCatalog.WarehouseStorekeeper, "View", "Create", "Submit", "Reserve", "Issue", "Close");

            await _context.RoleModuleActions.AddRangeAsync(srRows);
            await _context.SaveChangesAsync();
        }

        // Warehouse / Part Catalogue Request - مجوز Create/Review/Approve/Admin (Review/Admin در ماتریس ده‌اکشنی قدیمی نیست؛
        // به همین دلیل از همین مسیر RoleModuleAction می‌آید - همان الگوی Permit). نام ماژول «PartCatalogue» (بدون نقطه)
        // تا Permission.Name = "PartCatalogue.Review" توسط Parserهای نام-مجوز درست شکسته شود.
        // دسترسی واقعی هر گام علاوه بر این مجوز، «گیرنده‌ی گام بودن» (مدیر مستقیم/Pool تعیین‌شده) هم لازم دارد.
        // درخواست‌دهنده‌ها از واحدهای مختلف‌اند → Create/View برای همه‌ی نقش‌های کاری؛ Viewer فقط View.
        await SeedModuleAsync("PartCatalogue", Add =>
        {
            foreach (var role in new[]
            {
                SystemRoles.EngineeringManager, SystemRoles.EngineeringSupervisor, SystemRoles.DisciplineEngineer,
                SystemRoles.DocumentController, SystemRoles.MaintenanceEngineer, SystemRoles.ContractManager, SystemRoles.QAManager,
                SystemRoles.Legal, SystemRoles.Finance, SystemRoles.HSE,
                "MNT.Manager", "MNT.PlanningHead", "MNT.PlanningExpert", "MNT.DisciplineHead", "MNT.SeniorExpert", "MNT.Expert",
                "OPS.IssueAuthority", RoleCatalog.WarehouseStorekeeper
            })
                Add(role, "View", "Create");

            Add(RoleCatalog.WarehouseManager, "View", "Create", "Review", "Approve", "Admin");
            Add(SystemRoles.EngineeringManager, "Review", "Approve");
            Add(SystemRoles.EngineeringSupervisor, "Review");
            Add(SystemRoles.MaintenanceEngineer, "Review");
            Add(SystemRoles.ContractManager, "Approve");
            Add(SystemRoles.Viewer, "View");
        });
    }

    private async Task SeedModuleAsync(string module, Action<AddRoleActions> configure)
    {
        if (await _context.RoleModuleActions.AnyAsync(x => x.Module == module))
            return;

        var defaults = new List<RoleModuleAction>();

        void Add(string role, params string[] actions)
        {
            foreach (var action in actions)
                defaults.Add(new RoleModuleAction { RoleName = role, Module = module, Action = action, CreatedDate = DateTime.Now });
        }

        configure(Add);

        await _context.RoleModuleActions.AddRangeAsync(defaults);
        await _context.SaveChangesAsync();
    }
}
