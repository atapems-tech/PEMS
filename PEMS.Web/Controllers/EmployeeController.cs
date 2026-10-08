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

    // طبق سیاست رمز عبور سیستم (RequiredLength=8, RequireDigit=true) - رمز پیش‌فرض جدید
    public const string DefaultNewUserPassword = "Abc@123456";

    // طبق CMMS-10a ADR-01
    private static readonly string[] EmployeePersonnelTypes = { "Internal", "Contractor", "External", "Temporary" };

    public EmployeeController(PEMSDbContext context, UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager, PEMS.Application.Interfaces.IEmployeeLaborRateService laborRateService,
        PEMS.Application.Interfaces.IEmployeeSkillService employeeSkillService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _laborRateService = laborRateService;
        _employeeSkillService = employeeSkillService;
    }

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

        ViewBag.LaborRates = await _laborRateService.GetHistoryAsync(id);

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

        // Employee is the personnel master; authentication/roles belong to the linked ApplicationUser.
        // Expose the link here so administrators can see the account state without creating a second
        // role-management surface inside Employees.
        var linkedUser = await _context.Users.FirstOrDefaultAsync(x => x.EmployeeId == id);
        ViewBag.LinkedUser = linkedUser;
        ViewBag.LinkedUserRoles = linkedUser == null
            ? new List<string>()
            : (await _userManager.GetRolesAsync(linkedUser)).OrderBy(x => x).ToList();

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
        DateTime? hireDate, string? location, string? shift, string? homeAddress)
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

        // ==============================================================================
        // ساخت هم‌زمان یوزر - دقیقاً مثل فرم مستقل ساخت یوزر (Admin/Users/Create)، فقط با
        // رمز پیش‌فرض ثابت به‌جای رمز دستی، چون سازنده‌ی Employee معمولاً رمز واقعی کارمند
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

            var result = await _userManager.CreateAsync(newUser, DefaultNewUserPassword);

            if (!result.Succeeded)
            {
                TempData["Error"] = "کارمند ساخته شد ولی ساخت یوزر ناموفق بود: "
                    + string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            // فقط SystemAdmin مجاز است نقش SystemAdmin را به کاربر جدید تخصیص دهد - جلوگیری
            // از ارتقای دسترسی توسط EngineeringManager از این فرم
            if (selectedRoles != null && selectedRoles.Any())
            {
                if (!User.IsInRole(SystemRoles.SystemAdmin))
                    selectedRoles = selectedRoles.Where(r => r != SystemRoles.SystemAdmin).ToList();

                if (selectedRoles.Any())
                    var roleResult = await _userManager.AddToRolesAsync(newUser, selectedRoles);
                    if (!roleResult.Succeeded)
                    {
                        TempData["Error"] = "کارمند و یوزر ساخته شد، اما تخصیص برخی نقش‌ها ناموفق بود: "
                            + string.Join(", ", roleResult.Errors.Select(e => e.Description));
                        return RedirectToAction(nameof(Index));
                    }
            }

            await SyncRoleAssignmentsAsync(newUser.Id);

            TempData["Message"] = $"کارمند و یوزر «{userName}» ساخته شد (رمز پیش‌فرض: {DefaultNewUserPassword} - باید در اولین ورود عوض بشه).";
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
        bool returnToDetails = false)
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
        employee.Position = position;
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
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "فایلی انتخاب نشده.";
            return RedirectToAction(nameof(Index));
        }

        var departments = await _context.Departments.ToListAsync();
        var disciplines = await _context.Disciplines.ToListAsync();
        var existingCodes = new HashSet<string>(
            await _context.Employees.Where(x => x.PersonnelCode != "").Select(x => x.PersonnelCode).ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        int added = 0, skippedDuplicate = 0, skippedInvalid = 0;

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
                skippedInvalid++;
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

            var dept = departments.FirstOrDefault(d => d.Name.Equals(deptName, StringComparison.OrdinalIgnoreCase));
            var disc = disciplines.FirstOrDefault(d => d.Name.Equals(discName, StringComparison.OrdinalIgnoreCase));

            if (dept == null)
            {
                skippedInvalid++;
                continue;
            }

            await _context.Employees.AddAsync(new Employee
            {
                PersonnelCode = code,
                FirstName = firstName,
                LastName = lastName,
                Position = sheet.Cell(r, 4).GetString().Trim(),
                Email = sheet.Cell(r, 5).GetString().Trim(),
                Phone = sheet.Cell(r, 6).GetString().Trim(),
                DepartmentId = dept.DepartmentId,
                DisciplineId = disc?.DisciplineId ?? 0,
                IsActive = true,
                CreatedDate = DateTime.Now
            });

            if (code != "") existingCodes.Add(code);
            added++;
        }

        await _context.SaveChangesAsync();

        TempData["Message"] = $"ایمپورت تمام شد: {added} اضافه شد، {skippedDuplicate} تکراری رد شد، {skippedInvalid} نامعتبر رد شد.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Identity roles are the legacy/runtime membership surface; RoleAssignments is the live
    /// authorization projection. Keep direct global assignments synchronized so a role removal
    /// cannot leave stale authorization in AccessControlService.
    /// </summary>
    private async Task SyncRoleAssignmentsAsync(int userId)
    {
        var roleIds = await _context.UserRoles
            .Where(x => x.UserId == userId)
            .Select(x => x.RoleId)
            .ToListAsync();

        var assignments = await _context.RoleAssignments
            .Where(x => x.UserId == userId && x.AssignmentType == "Direct" && x.ScopeType == "Global")
            .ToListAsync();

        foreach (var assignment in assignments)
            assignment.IsActive = roleIds.Contains(assignment.RoleId);

        foreach (var roleId in roleIds)
        {
            if (assignments.Any(x => x.RoleId == roleId))
            {
                var existing = assignments.First(x => x.RoleId == roleId);
                existing.IsActive = true;
                continue;
            }

            await _context.RoleAssignments.AddAsync(new RoleAssignment
            {
                UserId = userId,
                RoleId = roleId,
                ScopeType = "Global",
                ScopeValue = null,
                AssignmentType = "Direct",
                IsActive = true,
                CreatedDate = DateTime.Now
            });
        }

        await _context.SaveChangesAsync();
    }

    private async Task LoadDropdownsAsync()
    {
        ViewBag.Departments = await _context.Departments.Where(x => x.IsActive)
            .Select(x => new { x.DepartmentId, x.Name }).ToListAsync();

        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();

        ViewBag.AllRoles = await _roleManager.Roles.Select(x => x.Name!).ToListAsync();

        // طبق CMMS-10a ADR-01: پرسنل غیر-Internal باید به یک Vendor وصل باشند
        ViewBag.Vendors = await _context.Vendors.Where(x => x.IsActive)
            .Select(x => new { x.VendorId, x.Name }).ToListAsync();
    }
}
