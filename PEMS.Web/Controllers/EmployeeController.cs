using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Web.Helpers;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق درخواست: هنگام ثبت Employee جدید، امکان ساخت هم‌زمان یک User با نقش‌های انتخابی
/// فراهم شده - دقیقاً مثل فرم ساخت یوزر مستقل (Admin/Users/Create)، با رمز پیش‌فرض
/// «abc@123» که کاربر باید در اولین ورود عوضش کند. همچنین جلوگیری از ثبت کد پرسنلی تکراری.
/// </summary>
[Authorize(Roles = SystemRoles.SystemAdmin + "," + SystemRoles.EngineeringManager)]
public class EmployeeController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly PEMS.Application.Interfaces.IEmployeeLaborRateService _laborRateService;
    private readonly PEMS.Application.Interfaces.IEmployeeSkillService _employeeSkillService;
    private readonly PEMS.Application.Interfaces.IUserRoleAssignmentSynchronizer _roleSync;
    private readonly PEMS.Application.Organization.IOrganizationHierarchyService _hier;
    private readonly Microsoft.Extensions.Localization.IStringLocalizer<PEMS.Web.Resources.SharedResource>? _l;

    // طبق CMMS-10a ADR-01
    private static readonly string[] EmployeePersonnelTypes = { "Internal", "Contractor", "External", "Temporary" };

    public EmployeeController(PEMSDbContext context, UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager, PEMS.Application.Interfaces.IEmployeeLaborRateService laborRateService,
        PEMS.Application.Interfaces.IEmployeeSkillService employeeSkillService,
        PEMS.Application.Interfaces.IUserRoleAssignmentSynchronizer? roleSync = null,
        PEMS.Application.Organization.IOrganizationHierarchyService? hierarchy = null,
        Microsoft.Extensions.Localization.IStringLocalizer<PEMS.Web.Resources.SharedResource>? localizer = null)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _laborRateService = laborRateService;
        _employeeSkillService = employeeSkillService;
        _roleSync = roleSync ?? new PEMS.Persistence.Services.UserRoleAssignmentSynchronizer(context);
        _hier = hierarchy ?? new PEMS.Persistence.Services.OrganizationHierarchyService(context, new PEMS.Persistence.Services.OrganizationValidationService(context));
        _l = localizer;
    }

    private string T(string key) => _l != null ? _l[key].Value : key;

    /// <summary>تغییر سمت سازمانی = تغییر ساختار (مدیر مستقیم/مسیر تأیید را عوض می‌کند)؛ فقط SystemAdmin (سمت سرور، نه فقط پنهان‌بودن دکمه).</summary>
    private bool CanAssignPosition => User.IsInRole(SystemRoles.SystemAdmin);

    // ==============================================================================
    // طبق سند معماری Employee/Team/Labor Cost: صفحه‌ی جزئیات کارمند - نرخ دستمزد و
    // تاریخچه‌ی عضویت تیم، بدون تکرار اطلاعات پرسنلی جای دیگه‌ای (Single Source of Truth)
    // ==============================================================================
    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "Employee Details";
        ViewData["Section"] = "Account";
        ViewData["HelpKey"] = "Employee.Details";

        var employee = await _context.Employees
            .Include(x => x.Department)
            .Include(x => x.Discipline)
            .Include(x => x.VendorRef)
            .FirstOrDefaultAsync(x => x.EmployeeId == id);

        if (employee == null) return NotFound();

        // طبق «Employee Details — Stitch UI Redesign»: Modal ویرایش پروفایل روی همین صفحه
        // همان Dropdown هایی را لازم دارد که Modal ویرایش داخل Index استفاده می‌کند
        await LoadDropdownsAsync();
        ViewBag.PrimaryPositions = await _hier.GetPrimaryPositionsAsync(new[] { id });

        ViewBag.LaborRates = await _laborRateService.GetHistoryAsync(id);

        // «Account & Authorization»: نقش به ApplicationUser تعلق دارد، نه Employee. این‌جا فقط نمایش + مسیر «Manage Access» به صفحه‌ی
        // Administration → Users (منبع یگانه‌ی مدیریت نقش)؛ هیچ مدیریت نقش دومی در Employee ساخته نشده است.
        var linkedUser = await _context.Users.AsNoTracking().Where(u => u.EmployeeId == id)
            .Select(u => new { u.Id, u.UserName, u.IsActive }).FirstOrDefaultAsync();
        if (linkedUser != null)
        {
            var roleNames = await (from ur in _context.UserRoles join r in _context.Roles on ur.RoleId equals r.Id where ur.UserId == linkedUser.Id orderby r.Name select r.Name!).ToListAsync();
            ViewBag.LinkedUser = new { linkedUser.Id, linkedUser.UserName, linkedUser.IsActive, Roles = roleNames };
        }

        // سمت سازمانی از تخصیص اصلیِ فعال (EmployeeAssignment) - نه متن آزاد Employee.Position
        ViewBag.OrgContext = await new PEMS.Persistence.Services.OrganizationResolver(_context).GetContextAsync(id);

        // طبق «Employee Details — Stitch UI Redesign»: Employee فیلد Manager مستقل ندارد -
        // «مدیر» از طریق سرپرست تیمِ فعلیِ کارمند (Team.SupervisorEmployeeId) مشتق می‌شود، نه
        // یک فیلد تکراری جدید (Single Source of Truth طبق سند معماری Employee/Team)
        ViewBag.TeamHistory = await _context.TeamMembers
            .Include(x => x.Team!).ThenInclude(t => t.SupervisorEmployee)
            .Include(x => x.Role)
            .Where(x => x.EmployeeId == id)
            .OrderByDescending(x => x.JoinedDate)
            .ToListAsync();

        ViewBag.EmployeeSkills = await _context.EmployeeSkills
            .Include(x => x.Skill)
            .Where(x => x.EmployeeId == id && x.IsActive)
            .OrderBy(x => x.Skill!.Name)
            .ToListAsync();

        ViewBag.AllSkills = await _context.Skills.Where(x => x.IsActive)
            .OrderBy(x => x.Name).Select(x => new { x.SkillId, x.Name }).ToListAsync();

        ViewBag.EmployeeQualifications = await _context.EmployeeQualifications
            .Include(x => x.Qualification)
            .Where(x => x.EmployeeId == id && x.IsActive)
            .OrderByDescending(x => x.IssueDate)
            .ToListAsync();

        ViewBag.AllQualifications = await _context.Qualifications.Where(x => x.IsActive)
            .OrderBy(x => x.Name).Select(x => new { x.QualificationId, x.Name }).ToListAsync();

        ViewBag.EmployeeCertifications = await _context.EmployeeCertifications
            .Include(x => x.Certification)
            .Where(x => x.EmployeeId == id && x.IsActive)
            .OrderByDescending(x => x.IssueDate)
            .ToListAsync();

        ViewBag.AllCertifications = await _context.Certifications.Where(x => x.IsActive)
            .OrderBy(x => x.Name).Select(x => new { x.CertificationId, x.Name }).ToListAsync();

        return View(employee);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSkill(int employeeId, int skillId, string proficiencyLevel, DateTime? certifiedDate)
    {
        try
        {
            await _employeeSkillService.AddOrReactivateAsync(employeeId, skillId, proficiencyLevel, certifiedDate);
            TempData["Message"] = "مهارت اضافه شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = employeeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSkill(int employeeSkillId, int employeeId)
    {
        try
        {
            await _employeeSkillService.RemoveAsync(employeeSkillId);
            TempData["Message"] = "مهارت حذف شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = employeeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQualification(int employeeId, int qualificationId, string? issuer, DateTime issueDate, DateTime? expiryDate)
    {
        await _context.EmployeeQualifications.AddAsync(new EmployeeQualification
        {
            EmployeeId = employeeId,
            QualificationId = qualificationId,
            Issuer = issuer,
            IssueDate = issueDate,
            ExpiryDate = expiryDate,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = "صلاحیت ثبت شد.";
        return RedirectToAction(nameof(Details), new { id = employeeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveQualification(int employeeQualificationId, int employeeId)
    {
        var eq = await _context.EmployeeQualifications.FirstOrDefaultAsync(x => x.EmployeeQualificationId == employeeQualificationId);
        if (eq != null)
        {
            eq.IsActive = false;
            await _context.SaveChangesAsync();
            TempData["Message"] = "صلاحیت حذف شد.";
        }

        return RedirectToAction(nameof(Details), new { id = employeeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCertification(int employeeId, int certificationId, string? certificateNumber, string? issuer, DateTime issueDate, DateTime? expiryDate)
    {
        await _context.EmployeeCertifications.AddAsync(new EmployeeCertification
        {
            EmployeeId = employeeId,
            CertificationId = certificationId,
            CertificateNumber = certificateNumber,
            Issuer = issuer,
            IssueDate = issueDate,
            ExpiryDate = expiryDate,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = "گواهینامه ثبت شد.";
        return RedirectToAction(nameof(Details), new { id = employeeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCertification(int employeeCertificationId, int employeeId)
    {
        var ec = await _context.EmployeeCertifications.FirstOrDefaultAsync(x => x.EmployeeCertificationId == employeeCertificationId);
        if (ec != null)
        {
            ec.IsActive = false;
            await _context.SaveChangesAsync();
            TempData["Message"] = "گواهینامه حذف شد.";
        }

        return RedirectToAction(nameof(Details), new { id = employeeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLaborRate(int employeeId, string rateType, decimal rate, string currency, DateTime effectiveFrom)
    {
        try
        {
            await _laborRateService.SetRateAsync(employeeId, rateType, rate, currency, effectiveFrom);
            TempData["Message"] = "نرخ دستمزد ثبت شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = employeeId });
    }

    // طبق «PEMS — Global Grid Hardening» P1: قبلاً کل جدول Employees (فیلترشده اما بدون
    // Skip/Take) با ۲ Include بارگذاری می‌شد و هر ردیف Modal ویرایش خودش را رندر می‌کرد -
    // در سازمانی با صدها/هزاران کارمند، این هم Query و هم HTML/DOM را نامحدود رشد می‌داد.
    // فیلترهای موجود (department/discipline/q) حفظ شدند؛ HasUser با Subquery همبسته محاسبه
    // می‌شود (نه پیش‌بارگذاری کل جدول Users). Export زیر عمداً دست‌نخورده ماند - جدا بررسی شد.
    public async Task<IActionResult> Index(int? departmentId, int? disciplineId, string? q, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Employees";
        ViewData["Section"] = "Administration";

        var query = _context.Employees.AsNoTracking().AsQueryable();

        if (departmentId.HasValue) query = query.Where(x => x.DepartmentId == departmentId.Value);
        if (disciplineId.HasValue) query = query.Where(x => x.DisciplineId == disciplineId.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(x => x.FirstName.ToLower().Contains(term) || x.LastName.ToLower().Contains(term)
                || x.PersonnelCode.ToLower().Contains(term) || (x.Email != null && x.Email.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync();
        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var normalizedPage = page < 1 ? 1 : page;

        var items = await query
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .ThenBy(x => x.EmployeeId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new EmployeeListItemDto
            {
                EmployeeId = x.EmployeeId,
                PersonnelCode = x.PersonnelCode,
                FirstName = x.FirstName,
                LastName = x.LastName,
                Position = x.Position,
                Email = x.Email,
                Phone = x.Phone,
                DepartmentId = x.DepartmentId,
                DepartmentName = x.Department != null ? x.Department.Name : null,
                DisciplineId = x.DisciplineId,
                DisciplineName = x.Discipline != null ? x.Discipline.Name : null,
                PersonnelType = x.PersonnelType,
                VendorId = x.VendorId,
                VendorName = x.VendorId != null
                    ? _context.Vendors.Where(v => v.VendorId == x.VendorId).Select(v => v.Name).FirstOrDefault()
                    : null,
                IsActive = x.IsActive,
                HireDate = x.HireDate,
                Location = x.Location,
                Shift = x.Shift,
                HomeAddress = x.HomeAddress
            })
            .ToListAsync();

        var result = new PagedResult<EmployeeListItemDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };

        await LoadDropdownsAsync();
        ViewBag.PrimaryPositions = await _hier.GetPrimaryPositionsAsync(items.Select(i => i.EmployeeId));

        ViewBag.SelectedDepartmentId = departmentId;
        ViewBag.SelectedDisciplineId = disciplineId;
        ViewBag.SearchTerm = q;

        return View(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string personnelCode, string firstName, string lastName, string? position,
        string? email, string? phone, int departmentId, int disciplineId,
        bool createUser, string? userName, List<string>? selectedRoles,
        string? personnelType, int? vendorId,
        DateTime? hireDate, string? location, string? shift, string? homeAddress, int? positionId = null)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            TempData["Error"] = "First and last name are required";
            return RedirectToAction(nameof(Index));
        }

        // طبق CMMS-10a ADR-01: پرسنل غیر-Internal باید به یک Vendor وصل باشند
        var resolvedPersonnelType = string.IsNullOrWhiteSpace(personnelType) ? "Internal" : personnelType;
        if (!EmployeePersonnelTypes.Contains(resolvedPersonnelType, StringComparer.Ordinal))
        {
            TempData["Error"] = "نوع پرسنل نامعتبره.";
            return RedirectToAction(nameof(Index));
        }
        if (resolvedPersonnelType != "Internal" && !vendorId.HasValue)
        {
            TempData["Error"] = "برای پرسنل غیر-Internal، انتخاب Vendor الزامی است.";
            return RedirectToAction(nameof(Index));
        }

        var orgError = await ValidateOrgRefsAsync(departmentId, disciplineId);
        if (orgError != null)
        {
            TempData["Error"] = orgError;
            return RedirectToAction(nameof(Index));
        }

        var trimmedCode = personnelCode?.Trim() ?? "";

        // طبق درخواست: تا مقادیر یکسان (کد پرسنلی) نباشد نباید ثبت بشه
        if (trimmedCode != "")
        {
            var duplicateCode = await _context.Employees.AnyAsync(x => x.PersonnelCode == trimmedCode);
            if (duplicateCode)
            {
                TempData["Error"] = $"کد پرسنلی «{trimmedCode}» قبلاً برای یک کارمند دیگر ثبت شده است.";
                return RedirectToAction(nameof(Index));
            }
        }

        if (createUser)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                TempData["Error"] = "برای ساخت یوزر، نام کاربری الزامی است.";
                return RedirectToAction(nameof(Index));
            }

            var existingUser = await _userManager.FindByNameAsync(userName.Trim());
            if (existingUser != null)
            {
                TempData["Error"] = $"نام کاربری «{userName}» قبلاً استفاده شده است.";
                return RedirectToAction(nameof(Index));
            }
        }

        var employee = new Employee
        {
            PersonnelCode = trimmedCode,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Position = position,
            Email = email,
            Phone = phone,
            DepartmentId = departmentId,
            DisciplineId = disciplineId,
            PersonnelType = resolvedPersonnelType,
            VendorId = resolvedPersonnelType == "Internal" ? null : vendorId,
            IsActive = true,
            CreatedDate = DateTime.Now,
            HireDate = hireDate,
            Location = location,
            Shift = shift,
            HomeAddress = homeAddress
        };

        await _context.Employees.AddAsync(employee);
        await _context.SaveChangesAsync();

        // سمت سازمانی از سرویس سازمانی مرکزی (قوانین: سمت فعال/خالی، یک سمت اصلی برای هر کارمند)؛ هرگز از متن آزاد ساخته نمی‌شود
        if (positionId.HasValue) await ApplyPositionAsync(employee, positionId, onlyWhenChanged: false);

        // ==============================================================================
        // ساخت هم‌زمان یوزر - دقیقاً مثل فرم مستقل ساخت یوزر (Admin/Users/Create)، فقط با
        // رمز یک‌بارمصرفِ تصادفی به‌جای رمز دستی، چون سازنده‌ی Employee معمولاً رمز واقعی کارمند
        // رو نمی‌دونه. کاربر با MustChangePassword=true علامت می‌خوره.
        // ==============================================================================
        if (createUser && !string.IsNullOrWhiteSpace(userName))
        {
            var newUser = new ApplicationUser
            {
                UserName = userName.Trim(),
                Email = email,
                EmailConfirmed = true,
                FullName = $"{employee.FirstName} {employee.LastName}",
                IsActive = true,
                EmployeeId = employee.EmployeeId,
                MustChangePassword = true,
                CreatedDate = DateTime.Now
            };

            // SCRUM-17: a random one-time password per user (shown once below), never a fixed default
            var temporaryPassword = PEMS.Shared.Security.TemporaryPasswordGenerator.Generate();
            var result = await _userManager.CreateAsync(newUser, temporaryPassword);

            if (!result.Succeeded)
            {
                TempData["Error"] = "کارمند ساخته شد ولی ساخت یوزر ناموفق بود: "
                    + string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            // فقط SystemAdmin مجاز است نقش SystemAdmin را به کاربر جدید تخصیص دهد - جلوگیری
            // از ارتقای دسترسی توسط EngineeringManager از این فرم
            IdentityResult? roleResult = null;
            if (selectedRoles != null && selectedRoles.Any())
            {
                if (!User.IsInRole(SystemRoles.SystemAdmin))
                    selectedRoles = selectedRoles.Where(r => r != SystemRoles.SystemAdmin).ToList();

                if (selectedRoles.Any())
                    roleResult = await _userManager.AddToRolesAsync(newUser, selectedRoles);
            }

            await _roleSync.SyncAsync(newUser.Id);

            if (roleResult != null && !roleResult.Succeeded)
            {
                TempData["Error"] = "کارمند و یوزر ساخته شد، اما تخصیص برخی نقش‌ها ناموفق بود: "
                    + string.Join(", ", roleResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            TempData["Message"] = $"کارمند و یوزر «{userName}» ساخته شد (رمز یک‌بارمصرف: {temporaryPassword} - فقط همین یک بار نمایش داده می‌شود و باید در اولین ورود عوض شود).";
        }
        else
        {
            TempData["Message"] = "Employee added";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int employeeId, string personnelCode, string firstName, string lastName,
        string? position, string? email, string? phone, int departmentId, int disciplineId,
        string? personnelType, int? vendorId,
        DateTime? hireDate, string? location, string? shift, string? homeAddress,
        bool returnToDetails = false, int? positionId = null, bool positionSubmitted = false)
    {
        // طبق «Employee Details — Stitch UI Redesign»: قبلاً Edit همیشه به Index برمی‌گشت،
        // چون تنها مسیر ویرایش Modal داخل Index بود. حالا صفحه‌ی Details هم Modal ویرایش
        // خودش را دارد - این پرچم (فقط وقتی از آن Modal بیاید) مسیر بازگشت را حفظ می‌کند،
        // بدون تغییر رفتار پیش‌فرض برای Modal قدیمیِ Index.
        IActionResult BackToCaller() =>
            returnToDetails ? RedirectToAction(nameof(Details), new { id = employeeId }) : RedirectToAction(nameof(Index));

        var employee = await _context.Employees.FirstOrDefaultAsync(x => x.EmployeeId == employeeId);
        if (employee == null) return NotFound();

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            TempData["Error"] = "First and last name are required";
            return BackToCaller();
        }

        var resolvedPersonnelType = string.IsNullOrWhiteSpace(personnelType) ? "Internal" : personnelType;
        if (!EmployeePersonnelTypes.Contains(resolvedPersonnelType, StringComparer.Ordinal))
        {
            TempData["Error"] = "نوع پرسنل نامعتبره.";
            return BackToCaller();
        }
        if (resolvedPersonnelType != "Internal" && !vendorId.HasValue)
        {
            TempData["Error"] = "برای پرسنل غیر-Internal، انتخاب Vendor الزامی است.";
            return BackToCaller();
        }

        var orgError = await ValidateOrgRefsAsync(departmentId, disciplineId, employee);
        if (orgError != null)
        {
            TempData["Error"] = orgError;
            return BackToCaller();
        }

        var trimmedCode = personnelCode?.Trim() ?? "";

        if (trimmedCode != "")
        {
            var duplicateCode = await _context.Employees
                .AnyAsync(x => x.EmployeeId != employeeId && x.PersonnelCode == trimmedCode);

            if (duplicateCode)
            {
                TempData["Error"] = $"کد پرسنلی «{trimmedCode}» قبلاً برای یک کارمند دیگر ثبت شده است.";
                return BackToCaller();
            }
        }

        employee.PersonnelCode = trimmedCode;
        employee.FirstName = firstName.Trim();
        employee.LastName = lastName.Trim();
        if (position != null) employee.Position = position;    // فرم‌ها دیگر متن آزاد نمی‌فرستند؛ مقدار میراثی بی‌صدا پاک نمی‌شود
        employee.Email = email;
        employee.Phone = phone;
        employee.DepartmentId = departmentId;
        employee.DisciplineId = disciplineId;
        employee.PersonnelType = resolvedPersonnelType;
        employee.VendorId = resolvedPersonnelType == "Internal" ? null : vendorId;
        employee.HireDate = hireDate;
        employee.Location = location;
        employee.Shift = shift;
        employee.HomeAddress = homeAddress;

        await _context.SaveChangesAsync();

        if (positionSubmitted) await ApplyPositionAsync(employee, positionId, onlyWhenChanged: true);

        TempData["Message"] = "Employee updated";
        return BackToCaller();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int employeeId)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(x => x.EmployeeId == employeeId);
        if (employee == null) return NotFound();

        employee.IsActive = !employee.IsActive;
        await _context.SaveChangesAsync();

        TempData["Message"] = employee.IsActive ? "Employee reactivated" : "Employee deactivated";
        return RedirectToAction(nameof(Index));
    }

    // ==============================================================================
    // Export/Import اکسل - طبق درخواست. ستون‌ها: PersonnelCode, FirstName, LastName,
    // Position, Email, Phone, Department (نام), Discipline (نام). Import فقط ردیف‌های جدید
    // (کد پرسنلی تکراری نباشد) را اضافه می‌کند و در انتها گزارش خلاصه می‌دهد.
    // ==============================================================================

    public async Task<IActionResult> Export()
    {
        var employees = await _context.Employees
            .Include(x => x.Department).Include(x => x.Discipline)
            .OrderBy(x => x.FirstName).ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Employees");
        sheet.AddStyledHeaderRow("PersonnelCode", "FirstName", "LastName", "Position", "Email", "Phone", "Department", "Discipline", "IsActive");

        int row = 2;
        foreach (var e in employees)
        {
            sheet.Cell(row, 1).Value = e.PersonnelCode;
            sheet.Cell(row, 2).Value = e.FirstName;
            sheet.Cell(row, 3).Value = e.LastName;
            sheet.Cell(row, 4).Value = e.Position;
            sheet.Cell(row, 5).Value = e.Email;
            sheet.Cell(row, 6).Value = e.Phone;
            sheet.Cell(row, 7).Value = e.Department?.Name;
            sheet.Cell(row, 8).Value = e.Discipline?.Name;
            sheet.Cell(row, 9).Value = e.IsActive;
            row++;
        }

        sheet.FinalizeExportSheet();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Employees_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PEMS.Web.Security.Uploads.UploadPolicy(PEMS.Application.Uploads.UploadProfile.Spreadsheet)]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "فایلی انتخاب نشده.";
            return RedirectToAction(nameof(Index));
        }

        var departments = await _context.Departments.Where(d => d.IsActive).ToListAsync();
        var disciplines = await _context.Disciplines.Where(d => d.IsActive).ToListAsync();
        var existingCodes = new HashSet<string>(
            await _context.Employees.Where(x => x.PersonnelCode != "").Select(x => x.PersonnelCode).ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        int added = 0, skippedDuplicate = 0, skippedInvalid = 0;
        var reasons = new List<string>();
        void Reject(int row, string why) { skippedInvalid++; if (reasons.Count < 10) reasons.Add($"#{row}: {why}"); }

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        for (int r = 2; r <= lastRow; r++)
        {
            var code = sheet.Cell(r, 1).GetString().Trim();
            var firstName = sheet.Cell(r, 2).GetString().Trim();
            var lastName = sheet.Cell(r, 3).GetString().Trim();

            if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName)) continue; // ردیف خالی

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                Reject(r, "نام/نام خانوادگی خالی");
                continue;
            }

            // طبق درخواست: تا مقادیر یکسان (کد پرسنلی) نباشد ثبت نشود
            if (code != "" && existingCodes.Contains(code))
            {
                skippedDuplicate++;
                continue;
            }

            var deptName = sheet.Cell(r, 7).GetString().Trim();
            var discName = sheet.Cell(r, 8).GetString().Trim();

            // تطبیق قطعی و بدون حدس: Code یا Name؛ دیسیپلین علاوه بر آن ImportAliases. فقط رکوردهای فعال؛ صفر یا بیش از یک تطبیق = رد ردیف (نه 0/null/Other)
            var deptMatches = departments.Where(d => d.Name.Equals(deptName, StringComparison.OrdinalIgnoreCase)
                                                     || (!string.IsNullOrEmpty(d.Code) && d.Code.Equals(deptName, StringComparison.OrdinalIgnoreCase))).ToList();
            if (deptMatches.Count == 0) { Reject(r, $"دپارتمان «{deptName}» یافت نشد/غیرفعال است"); continue; }
            if (deptMatches.Count > 1) { Reject(r, $"دپارتمان «{deptName}» مبهم است"); continue; }

            var discMatches = disciplines.Where(d => d.Name.Equals(discName, StringComparison.OrdinalIgnoreCase)
                                                     || d.Code.Equals(discName, StringComparison.OrdinalIgnoreCase)
                                                     || (!string.IsNullOrEmpty(d.ImportAliases) && d.ImportAliases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                                         .Any(a => a.Equals(discName, StringComparison.OrdinalIgnoreCase)))).ToList();
            if (discName == "") { Reject(r, "دیسیپلین الزامی است"); continue; }
            if (discMatches.Count == 0) { Reject(r, $"دیسیپلین «{discName}» یافت نشد/غیرفعال است"); continue; }
            if (discMatches.Count > 1) { Reject(r, $"دیسیپلین «{discName}» مبهم است (چند رکورد)"); continue; }

            await _context.Employees.AddAsync(new Employee
            {
                PersonnelCode = code,
                FirstName = firstName,
                LastName = lastName,
                Position = sheet.Cell(r, 4).GetString().Trim(),   // عنوان متنیِ میراثی (نمایشی)؛ سمت سازمانی واقعی از طریق Organization Import/EmployeeAssignment
                Email = sheet.Cell(r, 5).GetString().Trim(),
                Phone = sheet.Cell(r, 6).GetString().Trim(),
                DepartmentId = deptMatches[0].DepartmentId,
                DisciplineId = discMatches[0].DisciplineId,
                IsActive = true,
                CreatedDate = DateTime.Now
            });

            if (code != "") existingCodes.Add(code);
            added++;
        }

        await _context.SaveChangesAsync();

        TempData["Message"] = $"ایمپورت تمام شد: {added} اضافه شد، {skippedDuplicate} تکراری رد شد، {skippedInvalid} نامعتبر رد شد."
            + (reasons.Count > 0 ? " | " + string.Join(" ; ", reasons) : "");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>تخصیص/تغییر سمت اصلیِ کارمند (فقط SystemAdmin). خطا = پیام Warning؛ متن میراثی Employee.Position فقط به‌عنوان عنوان نمایشی هم‌گام می‌شود.</summary>
    private async Task ApplyPositionAsync(Employee employee, int? positionId, bool onlyWhenChanged)
    {
        if (!CanAssignPosition) { TempData["Warning"] = T("Emp_PositionAdminOnly"); return; }
        var r = await _hier.SetPrimaryPositionAsync(employee.EmployeeId, positionId);
        if (!r.Success) { TempData["Warning"] = T("Emp_PositionNotAssigned") + " " + T(r.ErrorKey ?? "Org_Err_Generic"); return; }
        if (positionId.HasValue)
        {
            var title = await _context.Positions.AsNoTracking().Where(p => p.PositionId == positionId.Value).Select(p => p.Title).FirstOrDefaultAsync();
            if (title != null && employee.Position != title) { employee.Position = title; await _context.SaveChangesAsync(); }
        }
    }

    /// <summary>Department/Discipline باید موجود و فعال باشند (مگر بدون تغییر نسبت به مقدار فعلیِ کارمند). خطا = پیام؛ هیچ FK نامعتبر ذخیره نمی‌شود.</summary>
    private async Task<string?> ValidateOrgRefsAsync(int departmentId, int disciplineId, Employee? current = null)
    {
        var deptOk = await _context.Departments.AnyAsync(d => d.DepartmentId == departmentId && (d.IsActive || (current != null && current.DepartmentId == departmentId)));
        if (!deptOk) return "دپارتمان نامعتبر یا غیرفعال است.";
        var discOk = await _context.Disciplines.AnyAsync(d => d.DisciplineId == disciplineId && (d.IsActive || (current != null && current.DisciplineId == disciplineId)));
        if (!discOk) return "دیسیپلین نامعتبر یا غیرفعال است (مقدار 0/ناموجود پذیرفته نمی‌شود).";
        return null;
    }

    private async Task LoadDropdownsAsync()
    {
        ViewBag.Departments = await _context.Departments.Where(x => x.IsActive)
            .Select(x => new { x.DepartmentId, x.Name }).ToListAsync();

        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();

        ViewBag.AllRoles = await _roleManager.Roles.Select(x => x.Name!).ToListAsync();

        // سمت سازمانی: فقط سمت‌های فعالِ خالی؛ سمت اصلیِ فعلیِ هر کارمند جدا از Employee.Position (متن میراثی) از EmployeeAssignment می‌آید
        ViewBag.CanAssignPosition = CanAssignPosition;
        ViewBag.VacantPositions = await _hier.GetVacantPositionsAsync();
        ViewBag.PrimaryPositions = new Dictionary<int, (int PositionId, string Label)>();
        ViewBag.AllDisciplineNames = await _context.Disciplines.AsNoTracking().ToDictionaryAsync(d => d.DisciplineId, d => d.Name);
        ViewBag.AllDepartmentNames = await _context.Departments.AsNoTracking().ToDictionaryAsync(d => d.DepartmentId, d => d.Name);

        // طبق CMMS-10a ADR-01: پرسنل غیر-Internal باید به یک Vendor وصل باشند
        ViewBag.Vendors = await _context.Vendors.Where(x => x.IsActive)
            .Select(x => new { x.VendorId, x.Name }).ToListAsync();
    }
}
