using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Persistence.Identity;
using PEMS.Web.Areas.Admin.Models;
using PEMS.Web.Helpers;

namespace PEMS.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly PEMS.Persistence.Context.PEMSDbContext _context;

    // طبق سند «PEMS — Global Data Grid Standard» بند ۶: Export هم باید یک سقف امن داشته باشد
    // (همون الگوی DocumentRegisterController.MaxExportRows) - نه واقعاً نامحدود. تعداد کاربران
    // امروز خیلی کمتر از این سقف است؛ این فقط از رشد بی‌کنترل آینده جلوگیری می‌کند.
    private const int MaxExportRows = 20000;


    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        PEMS.Persistence.Context.PEMSDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    private async Task<int> CreateEmployeeFromModelAsync(UserFormViewModel model)
    {
        var nameParts = model.FullName.Trim().Split(' ', 2);

        var newEmployee = new PEMS.Domain.Entities.Employee
        {
            FirstName = nameParts.Length > 0 ? nameParts[0] : model.FullName,
            LastName = nameParts.Length > 1 ? nameParts[1] : "",
            Position = model.Position,
            Email = model.Email,
            DepartmentId = model.DepartmentId!.Value,
            DisciplineId = model.DisciplineId ?? 0,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.Employees.AddAsync(newEmployee);
        await _context.SaveChangesAsync();

        return newEmployee.EmployeeId;
    }

    private async Task LoadEmployeeDropdownsAsync(UserFormViewModel model)
    {
        var employees = await _context.Employees.Where(x => x.IsActive)
            .Select(x => new { x.EmployeeId, Name = x.FirstName + " " + x.LastName })
            .ToListAsync();
        model.AllEmployees = employees.Select(x => (x.EmployeeId, x.Name)).ToList();

        var departments = await _context.Departments.Where(x => x.IsActive)
            .Select(x => new { x.DepartmentId, x.Name })
            .ToListAsync();
        model.AllDepartments = departments.Select(x => (x.DepartmentId, x.Name)).ToList();

        var disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name })
            .ToListAsync();
        model.AllDisciplines = disciplines.Select(x => (x.DisciplineId, x.Name)).ToList();
    }



    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - P1): قبلاً pageSize=25 با
    // const محلی Hardcode شده بود، نه از QueryString.
    public async Task<IActionResult> Index(string? search, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Users";
        ViewData["Section"] = "Administration";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);

        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();

            // نقش‌ها روی جدول جدایی هستن؛ برای این‌که جستجو مثل قبل شامل نام نقش هم بشه،
            // آی‌دی کاربرهایی که نقش مطابق دارن رو جدا پیدا می‌کنیم (بدون N+1)
            var matchingUserIdsByRole = await _context.UserRoles
                .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name })
                .Where(x => x.RoleName != null && x.RoleName.ToLower().Contains(term))
                .Select(x => x.UserId)
                .ToListAsync();

            query = query.Where(u =>
                (u.UserName != null && u.UserName.ToLower().Contains(term)) ||
                u.FullName.ToLower().Contains(term) ||
                (u.Email != null && u.Email.ToLower().Contains(term)) ||
                matchingUserIdsByRole.Contains(u.Id));
        }

        var total = await query.CountAsync();

        var users = await query
            .OrderBy(u => u.UserName)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

        // یک کوئری برای نقش‌های کاربرهای همین صفحه، به‌جای یک کوئری جداگانه به ازای هر کاربر
        var userIds = users.Select(u => u.Id).ToList();
        var rolesByUserId = (await _context.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync())
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());

        var items = users.Select(user => new UserListItemViewModel
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            Email = user.Email,
            FullName = user.FullName,
            IsActive = user.IsActive,
            LockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
            Roles = rolesByUserId.TryGetValue(user.Id, out var roles) ? roles : new List<string>(),
            LastLoginDate = user.LastLoginDate
        }).ToList();

        ViewBag.SearchTerm = search;

        return View(new PagedResult<UserListItemViewModel>
        {
            Items = items,
            Page = page,
            PageSize = normalizedPageSize,
            TotalCount = total
        });
    }



    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "New User";
        ViewData["Section"] = "Administration";

        var model = new UserFormViewModel
        {
            AllRoles = await _roleManager.Roles.Select(x => x.Name!).ToListAsync()
        };

        await LoadEmployeeDropdownsAsync(model);

        return View(model);
    }



    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel model)
    {
        model.AllRoles = _roleManager.Roles.Select(x => x.Name!).ToList();

        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Password is required for a new user.");

        // طبق بررسی: کاربر جدید باید یا به یک کارمند موجود وصل شود، یا دپارتمانش برای ساخت
        // کارمند جدید مشخص باشد - وگرنه محدودیت دیسیپلین، ارجاع نامه، تخصیص وظیفه و همه‌ی
        // چیزهایی که به Employee وابسته‌اند برای این کاربر کار نمی‌کنند
        if (!model.ExistingEmployeeId.HasValue && !model.DepartmentId.HasValue)
            ModelState.AddModelError(nameof(model.DepartmentId), "Select an existing employee or specify a department to create one.");

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "New User";
            ViewData["Section"] = "Administration";

            await LoadEmployeeDropdownsAsync(model);
            return View(model);
        }


        int? employeeId = model.ExistingEmployeeId;

        if (!employeeId.HasValue && model.DepartmentId.HasValue)
            employeeId = await CreateEmployeeFromModelAsync(model);

        var user = new ApplicationUser
        {
            UserName = model.UserName,
            Email = model.Email,
            EmailConfirmed = true,
            FullName = model.FullName,
            IsActive = model.IsActive,
            EmployeeId = employeeId,
            CreatedDate = DateTime.Now
        };

        var result = await _userManager.CreateAsync(user, model.Password!);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            ViewData["Title"] = "New User";
            ViewData["Section"] = "Administration";

            await LoadEmployeeDropdownsAsync(model);
            return View(model);
        }

        if (model.SelectedRoles.Any())
            await _userManager.AddToRolesAsync(user, model.SelectedRoles);

        TempData["Message"] = "User created successfully";

        return RedirectToAction(nameof(Index));
    }



    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "Edit User";
        ViewData["Section"] = "Administration";

        var user = await _userManager.FindByIdAsync(id.ToString());

        if (user == null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);

        var model = new UserFormViewModel
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            Email = user.Email ?? "",
            FullName = user.FullName,
            IsActive = user.IsActive,
            SelectedRoles = roles.ToList(),
            AllRoles = _roleManager.Roles.Select(x => x.Name!).ToList(),
            ExistingEmployeeId = user.EmployeeId
        };

        await LoadEmployeeDropdownsAsync(model);

        return View(model);
    }



    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserFormViewModel model)
    {
        model.AllRoles = _roleManager.Roles.Select(x => x.Name!).ToList();

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edit User";
            ViewData["Section"] = "Administration";

            await LoadEmployeeDropdownsAsync(model);
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.Id.ToString());

        if (user == null)
            return NotFound();

        user.Email = model.Email;
        user.FullName = model.FullName;
        user.IsActive = model.IsActive;

        // طبق بررسی: امکان اصلاح/تنظیم ارتباط با کارمند بعد از ساخت هم فراهم شد - برای کاربران
        // قدیمی که قبل از این اصلاح ساخته شده بودند و اصلاً کارمند وصل نداشتند
        if (model.ExistingEmployeeId.HasValue)
        {
            user.EmployeeId = model.ExistingEmployeeId.Value;
        }
        else if (model.DepartmentId.HasValue)
        {
            user.EmployeeId = await CreateEmployeeFromModelAsync(model);
        }

        await _userManager.UpdateAsync(user);


        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            await _userManager.ResetPasswordAsync(user, token, model.Password);
        }


        var currentRoles = await _userManager.GetRolesAsync(user);

        var rolesToRemove = currentRoles.Except(model.SelectedRoles).ToList();

        var rolesToAdd = model.SelectedRoles.Except(currentRoles).ToList();

        if (rolesToRemove.Any())
            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);

        if (rolesToAdd.Any())
            await _userManager.AddToRolesAsync(user, rolesToAdd);


        TempData["Message"] = "User updated successfully";

        return RedirectToAction(nameof(Index));
    }



    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());

        if (user == null)
            return NotFound();

        user.IsActive = !user.IsActive;

        await _userManager.UpdateAsync(user);

        return RedirectToAction(nameof(Index));
    }


    // طبق سند «PEMS — Global Data Grid Standard» بند A-9: Pagination گرید نباید به‌طور
    // تصادفی روی Export اعمال شود (معنای Export = همه، نه فقط صفحه‌ی فعلی) - اما Export هم
    // نباید واقعاً نامحدود باشد. اینجا فقط یک سقف امن اضافه شده (MaxExportRows)؛ Authorization
    // (SystemAdmin، سطح کلاس) دست‌نخورده ماند.
    public async Task<IActionResult> Export()
    {
        var totalUsers = await _userManager.Users.CountAsync();
        if (totalUsers > MaxExportRows)
        {
            return BadRequest(
                $"تعداد کاربران ({totalUsers}) از سقف امن Export ({MaxExportRows}) بیشتر است. " +
                "لطفاً با مدیر سیستم برای Export دسته‌ای تماس بگیرید.");
        }

        var users = await _userManager.Users.ToListAsync();

        var rolesByUserId = (await _context.UserRoles
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync())
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Users");
        sheet.AddStyledHeaderRow("UserName", "FullName", "Email", "Roles", "IsActive", "LastLoginDate");

        int row = 2;
        foreach (var u in users)
        {
            var roles = rolesByUserId.TryGetValue(u.Id, out var r2) ? r2 : new List<string>();

            sheet.Cell(row, 1).Value = u.UserName;
            sheet.Cell(row, 2).Value = u.FullName;
            sheet.Cell(row, 3).Value = u.Email;
            sheet.Cell(row, 4).Value = string.Join(", ", roles);
            sheet.Cell(row, 5).Value = u.IsActive;
            if (u.LastLoginDate.HasValue) sheet.Cell(row, 6).Value = u.LastLoginDate.Value;
            row++;
        }

        sheet.FinalizeExportSheet();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Users_{DateTime.Now:yyyyMMdd}.xlsx");
    }
}
