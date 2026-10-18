using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Services;

// «TEST-PCAT» - حساب‌های تست مجازی برای گردش‌کار درخواست کاتالوگ قطعه (Part Catalogue Request).
//
// قواعد این ابزار:
//  * پیش‌فرض Dry-run است (فقط می‌خواند و برنامه را چاپ می‌کند)؛ نوشتن فقط با --apply.
//  * دیتابیس هدف باید صریحاً با PEMS_TEST_CONNECTION داده شود (پیش‌فرض ندارد) و باید محلی و دارای PEMSDB در نام باشد.
//  * فقط «افزودن» است: هیچ رکورد موجودی (کارمند/کاربر/نقش/پیکربندی مسئولیت/Pool دیگران) ویرایش یا حذف نمی‌شود.
//    تشخیص رکورد تست: PersonnelCode «TEST-PCAT-nnn»، نام کاربری «test-pcat-*»، نقش «TEST.PCAT.*»، کد سمت «TEST-PCAT-POS-*»، دپارتمان/دیسیپلین «TESTPCAT».
//  * ساخت کاربر از مسیر UserManager واقعی؛ رمز تصادفی تولید و فقط در فایل خصوصی کاربر (خارج از مخزن) نوشته می‌شود، نه در کد/خروجی Git.
//  * نقش‌های موجودِ مورد نیاز «پیکربندی فعلی مسئولیت‌ها» (EngineeringManager، MNT.Manager، WH.Manager) فقط به کاربران تست «داده» می‌شوند؛
//    پیکربندی مسئولیت‌ها تغییر نمی‌کند.

var apply = args.Contains("--apply");
var resetPasswords = args.Contains("--reset-passwords");
var cs = Environment.GetEnvironmentVariable("PEMS_TEST_CONNECTION");
if (string.IsNullOrWhiteSpace(cs)) { Console.Error.WriteLine("PEMS_TEST_CONNECTION is required (no default database)."); return 2; }
if (!cs.Contains("PEMSDB", StringComparison.OrdinalIgnoreCase) || !(cs.Contains("localhost", StringComparison.OrdinalIgnoreCase) || cs.Contains("127.0.0.1")))
{ Console.Error.WriteLine("Safety: only a local PEMSDB* development/test database is allowed."); return 3; }

var services = new ServiceCollection();
services.AddDbContext<PEMSDbContext>(o => o.UseSqlServer(cs));
services.AddLogging();
services.AddDataProtection();
services.AddIdentityCore<ApplicationUser>(o =>
    {
        o.Password.RequiredLength = 8; o.Password.RequireDigit = true; o.Password.RequireUppercase = false; o.Password.RequireNonAlphanumeric = false;
        o.User.RequireUniqueEmail = true;
    })
    .AddRoles<ApplicationRole>().AddEntityFrameworkStores<PEMSDbContext>().AddDefaultTokenProviders();
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<PEMSDbContext>();
var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
var hierarchy = new OrganizationHierarchyService(db, new OrganizationValidationService(db));
var sync = new UserRoleAssignmentSynchronizer(db);

var dbName = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs).InitialCatalog;
Console.WriteLine($"Target database: {dbName}   mode: {(apply ? "APPLY" : "DRY-RUN (no changes)")}");
var log = new List<string>();
void Say(string kind, string text) { var l = $"  [{kind,-7}] {text}"; Console.WriteLine(l); log.Add(l); }

// ---------------------------------------------------------------- definitions
const string RoleRequester = "TEST.PCAT.Requester", RoleApprover = "TEST.PCAT.Approver", RoleReviewer = "TEST.PCAT.Reviewer";
var testRoles = new (string Name, string Display, string Desc, string[] Actions)[]
{
    (RoleRequester, "TEST-PCAT Requester", "Test role: may view and create Part Catalogue requests only (no review/approval).", new[] { "View", "Create" }),
    (RoleApprover,  "TEST-PCAT Approver",  "Test role: Part Catalogue View + Approve only.", new[] { "View", "Approve" }),
    (RoleReviewer,  "TEST-PCAT Reviewer",  "Test role: Part Catalogue View + Review only.", new[] { "View", "Review" }),
};

// Name, code, user, step/pool key, roles, position code/title, parent position code
var people = new (string First, string Last, string Code, string User, string Title, string[] Roles, string? PoolKey, string PosCode, string? ParentPos)[]
{
    ("Jordan",  "Blake",  "TEST-PCAT-001", "test-pcat-requester", "Test Requester",              new[] { RoleRequester },                              null,                    "TEST-PCAT-POS-REQ",   "TEST-PCAT-POS-DM"),
    ("Alex",    "Morgan", "TEST-PCAT-002", "test-pcat-dm",        "Test Direct Manager",         new[] { "EngineeringManager" },                       "DirectManager",         "TEST-PCAT-POS-DM",    null),
    ("Daniel",  "Reed",   "TEST-PCAT-003", "test-pcat-maint",     "Test Maintenance Supervisor", new[] { "MNT.Manager", RoleReviewer, RoleApprover },   null,                    "TEST-PCAT-POS-MAINT", null),
    ("Sarah",   "Bennett","TEST-PCAT-004", "test-pcat-parts",     "Test Parts Specialist",       new[] { RoleReviewer },                               "SparePartsSpecialist",  "TEST-PCAT-POS-PARTS", null),
    ("Emily",   "Carter", "TEST-PCAT-005", "test-pcat-commodity", "Test Commodity Group Reviewer", new[] { RoleReviewer },                           "CommodityGroupReview",  "TEST-PCAT-POS-COMM",  null),
    ("Michael", "Foster", "TEST-PCAT-006", "test-pcat-cataloguer","Test Part Cataloguer",        new[] { "WH.Manager" },                               "PartCataloguer",        "TEST-PCAT-POS-CAT",   null),
    ("Chloe",   "Turner", "TEST-PCAT-007", "test-pcat-sourcing",  "Test Sourcing Unit",          new[] { RoleApprover },                               "SourcingUnit",          "TEST-PCAT-POS-SRC",   null),
};

string NewPassword()
{
    const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnopqrstuvwxyz", digits = "23456789", sym = "#$%&*";
    var all = upper + lower + digits;
    var chars = new List<char> { upper[RandomNumberGenerator.GetInt32(upper.Length)], lower[RandomNumberGenerator.GetInt32(lower.Length)],
                                 digits[RandomNumberGenerator.GetInt32(digits.Length)], sym[RandomNumberGenerator.GetInt32(sym.Length)] };
    while (chars.Count < 16) chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
    return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
}

// ---------------------------------------------------------------- department & discipline (test-only)
var dept = await db.Departments.FirstOrDefaultAsync(d => d.Code == "TESTPCAT");
if (dept == null)
{
    Say(apply ? "CREATE" : "WOULD", "Department TESTPCAT 'TEST-PCAT Test Department'");
    if (apply) { dept = new Department { Code = "TESTPCAT", Name = "TEST-PCAT Test Department", Description = "Virtual test department for Part Catalogue workflow tests (TEST-PCAT).", IsActive = true }; db.Departments.Add(dept); await db.SaveChangesAsync(); }
}
else Say("REUSE", $"Department TESTPCAT (id {dept.DepartmentId})");

var disc = await db.Disciplines.FirstOrDefaultAsync(d => d.Code == "TESTPCAT");
if (disc == null)
{
    Say(apply ? "CREATE" : "WOULD", "Discipline TESTPCAT 'TEST-PCAT Test Discipline'");
    if (apply)
    {
        var r = await new DisciplineMasterService(db).CreateAsync("TESTPCAT", "TEST-PCAT Test Discipline", "Virtual test discipline for Part Catalogue workflow tests (TEST-PCAT).", null, null);
        disc = await db.Disciplines.FirstOrDefaultAsync(d => d.Code == "TESTPCAT");
        if (disc == null) { Console.Error.WriteLine("Discipline creation failed: " + System.Text.Json.JsonSerializer.Serialize(r)); return 4; }
    }
}
else Say("REUSE", $"Discipline TESTPCAT (id {disc.DisciplineId})");

// ---------------------------------------------------------------- test roles (+ PartCatalogue actions)
foreach (var r in testRoles)
{
    if (await roles.FindByNameAsync(r.Name) == null)
    {
        Say(apply ? "CREATE" : "WOULD", $"Role {r.Name} -> PartCatalogue: {string.Join(", ", r.Actions)}");
        if (apply) await roles.CreateAsync(new ApplicationRole(r.Name) { DisplayName = r.Display, Description = r.Desc });
    }
    else Say("REUSE", $"Role {r.Name}");
    foreach (var a in r.Actions)
        if (!await db.RoleModuleActions.AnyAsync(x => x.RoleName == r.Name && x.Module == "PartCatalogue" && x.Action == a))
        {
            if (apply) { db.RoleModuleActions.Add(new RoleModuleAction { RoleName = r.Name, Module = "PartCatalogue", Action = a, CreatedDate = DateTime.Now }); await db.SaveChangesAsync(); }
        }
}

// ---------------------------------------------------------------- employees, positions, assignments, users, roles, pools
var posIds = new Dictionary<string, int>();
var credentials = new List<string>();
var register = new List<string>();

async Task<int?> EnsurePositionAsync(string code, string title, int? parentId)
{
    var p = await db.Positions.FirstOrDefaultAsync(x => x.PositionCode == code);
    if (p != null) { Say("REUSE", $"Position {code} (id {p.PositionId})"); return p.PositionId; }
    Say(apply ? "CREATE" : "WOULD", $"Position {code} '{title}'{(parentId.HasValue ? $" (parent id {parentId})" : "")}");
    if (!apply) return null;
    var res = await hierarchy.CreatePositionAsync(title, code, dept!.DepartmentId, "TEST-PCAT virtual position", parentId);
    if (!res.Success) { Console.Error.WriteLine($"Position {code} failed: {res.ErrorKey} {res.Detail}"); return null; }
    return (await db.Positions.FirstAsync(x => x.PositionCode == code)).PositionId;
}

// parent (direct-manager) position first, then the rest
foreach (var pe in people.OrderBy(p => p.ParentPos == null ? 0 : 1))
{
    int? parentId = pe.ParentPos != null && posIds.TryGetValue(pe.ParentPos, out var pid) ? pid : null;
    var id = await EnsurePositionAsync(pe.PosCode, pe.Title, parentId);
    if (id.HasValue) posIds[pe.PosCode] = id.Value;
}

foreach (var pe in people)
{
    Console.WriteLine($"- {pe.First} {pe.Last} ({pe.Code}, {pe.User})");
    var emp = await db.Employees.FirstOrDefaultAsync(e => e.PersonnelCode == pe.Code);
    var empCreated = false;
    if (emp == null)
    {
        Say(apply ? "CREATE" : "WOULD", $"Employee {pe.Code} {pe.First} {pe.Last}");
        if (apply)
        {
            emp = new Employee { PersonnelCode = pe.Code, FirstName = pe.First, LastName = pe.Last, Email = $"{pe.User}@pems-test.local", DepartmentId = dept!.DepartmentId, DisciplineId = disc!.DisciplineId, Position = pe.Title, IsActive = true, CreatedDate = DateTime.Now };
            db.Employees.Add(emp); await db.SaveChangesAsync(); empCreated = true;
        }
    }
    else Say("REUSE", $"Employee {pe.Code} (id {emp.EmployeeId})");

    // سمت اصلی (EmployeeAssignment) از سرویس سازمانی مرکزی
    if (emp != null && posIds.TryGetValue(pe.PosCode, out var posId))
    {
        var has = await db.EmployeeAssignments.AnyAsync(a => a.EmployeeId == emp.EmployeeId && a.PositionId == posId && a.IsActive && a.IsPrimary);
        if (!has)
        {
            Say(apply ? "ASSIGN" : "WOULD", $"primary position {pe.PosCode}");
            if (apply) { var r = await hierarchy.SetPrimaryPositionAsync(emp.EmployeeId, posId); if (!r.Success) Say("ERROR", $"assign failed: {r.ErrorKey} {r.Detail}"); }
        }
    }

    // کاربر برنامه
    var user = await users.FindByNameAsync(pe.User);
    string? pwdNote = null;
    if (user == null)
    {
        Say(apply ? "CREATE" : "WOULD", $"User {pe.User} -> roles {string.Join(", ", pe.Roles)}");
        if (apply && emp != null)
        {
            var pwd = NewPassword();
            user = new ApplicationUser { UserName = pe.User, Email = $"{pe.User}@pems-test.local", EmailConfirmed = true, FullName = $"{pe.First} {pe.Last} ({pe.Title})", EmployeeId = emp.EmployeeId, DisciplineId = disc!.DisciplineId, IsActive = true, MustChangePassword = false, CreatedDate = DateTime.Now };
            var res = await users.CreateAsync(user, pwd);
            if (!res.Succeeded) { Say("ERROR", "user create failed: " + string.Join("; ", res.Errors.Select(e => e.Description))); user = null; }
            else { credentials.Add($"{pe.User}\t{pwd}"); pwdNote = "new password written to the private credentials file"; }
        }
    }
    else
    {
        Say("REUSE", $"User {pe.User} (id {user.Id})");
        if (resetPasswords && apply)
        {
            var pwd = NewPassword(); var tok = await users.GeneratePasswordResetTokenAsync(user);
            var rr = await users.ResetPasswordAsync(user, tok, pwd);
            if (rr.Succeeded) { credentials.Add($"{pe.User}\t{pwd}"); pwdNote = "password reset"; } else Say("ERROR", "reset failed");
        }
    }

    if (user != null && apply)
    {
        if (user.EmployeeId != emp?.EmployeeId) Say("WARN", $"user {pe.User} is linked to employee {user.EmployeeId}, expected {emp?.EmployeeId}");
        var current = await users.GetRolesAsync(user);
        var missing = pe.Roles.Where(r => !current.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0) { await users.AddToRolesAsync(user, missing); Say("ROLES", "added " + string.Join(", ", missing)); }
        await sync.SyncAsync(user.Id);
    }

    // Pool مسئولیت (فقط افزودن ردیف جدید برای کارمند تست)
    if (pe.PoolKey != null && emp != null)
    {
        var inPool = await db.PartCatalogueApproverAssignments.AnyAsync(a => a.StepKey == pe.PoolKey && a.EmployeeId == emp.EmployeeId && a.IsActive);
        if (!inPool) { Say(apply ? "POOL" : "WOULD", $"add to approver pool '{pe.PoolKey}'"); if (apply) { db.PartCatalogueApproverAssignments.Add(new PartCatalogueApproverAssignment { StepKey = pe.PoolKey, EmployeeId = emp.EmployeeId, IsActive = true }); await db.SaveChangesAsync(); } }
        else Say("REUSE", $"pool '{pe.PoolKey}' membership");
    }
    register.Add($"{pe.First} {pe.Last}|{pe.Code}|{emp?.EmployeeId}|{pe.User}|{user?.Id}|{pe.Title}|{string.Join(",", pe.Roles)}|{pe.PoolKey ?? "(role-based)"}|{(empCreated ? "created" : "reused/dry")}{(pwdNote != null ? "; " + pwdNote : "")}");
}

// ---------------------------------------------------------------- responsibility position lists (additive only)
// «MaintenanceSupervisor» در پیکربندی فعلی از نوع PositionScope با فهرست سمت‌های مشخص است؛ هر سمت یک دارنده‌ی فعال دارد
// (سمت‌های موجود در اختیار کارمند دیگری‌اند). سمت تست به انتهای فهرست «افزوده» می‌شود؛ ردیف‌های موجود دست‌نخورده می‌مانند.
foreach (var (key, posCode) in new[] { ("MaintenanceSupervisor", "TEST-PCAT-POS-MAINT") })
{
    var resp = await db.WorkflowActorResponsibilities.Include(r => r.Positions).FirstOrDefaultAsync(r => r.ResponsibilityKey == key);
    if (resp == null) { Say("SKIP", $"responsibility '{key}' does not exist"); continue; }
    if (resp.Kind != PEMS.Domain.Entities.ActorResponsibilityKinds.PositionScope) { Say("SKIP", $"'{key}' is {resp.Kind}, not PositionScope"); continue; }
    if (!posIds.TryGetValue(posCode, out var pId)) { Say("WOULD", $"add position {posCode} to '{key}' position list"); continue; }
    if (resp.Positions.Any(p => p.PositionId == pId)) { Say("REUSE", $"'{key}' already lists {posCode}"); continue; }
    Say(apply ? "CONFIG" : "WOULD", $"append position {posCode} to '{key}' position list (existing {resp.Positions.Count} entries untouched)");
    if (apply) { db.WorkflowActorResponsibilityPositions.Add(new WorkflowActorResponsibilityPosition { WorkflowActorResponsibilityId = resp.WorkflowActorResponsibilityId, PositionId = pId, Priority = (resp.Positions.Count == 0 ? 0 : resp.Positions.Max(p => p.Priority)) + 1 }); await db.SaveChangesAsync(); }
}

// ---------------------------------------------------------------- outputs
Console.WriteLine();
Console.WriteLine("REGISTER (name|code|employeeId|username|userId|title|roles|responsibility|status)");
register.ForEach(Console.WriteLine);
if (apply && credentials.Count > 0)
{
    var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pems-test");
    Directory.CreateDirectory(dir);
    var file = Path.Combine(dir, $"pcat-test-accounts-{dbName}.txt");
    File.AppendAllText(file, $"# {DateTime.Now:yyyy-MM-dd HH:mm} database={dbName}  (private - do not commit)\r\n" + string.Join("\r\n", credentials) + "\r\n", Encoding.UTF8);
    Console.WriteLine($"Credentials (private, outside the repository): {file}");
}
return 0;
