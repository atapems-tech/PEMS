using System.ComponentModel.DataAnnotations;

namespace PEMS.Web.Areas.Admin.Models;

public class UserListItemViewModel
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public bool LockedOut { get; set; }

    public List<string> Roles { get; set; } = new();

    public DateTime? LastLoginDate { get; set; }
}


public class UserFormViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    // فقط برای ساخت کاربر جدید الزامی است
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    public bool IsActive { get; set; } = true;

    public List<string> SelectedRoles { get; set; } = new();

    public List<string> AllRoles { get; set; } = new();

    // طبق بررسی: قبلاً ساخت کاربر هیچ ارتباطی با کارمند/دپارتمان نداشت - یعنی محدودیت دیسیپلین،
    // ارجاع نامه، تخصیص وظیفه و... همه‌ی این‌ها که به Employee وابسته‌اند برای کاربر جدید کار
    // نمی‌کردند. حالا یا یک کارمند موجود انتخاب می‌شود، یا یک کارمند جدید همین‌جا ساخته می‌شود.
    public int? ExistingEmployeeId { get; set; }

    [Display(Name = "Department")]
    public int? DepartmentId { get; set; }

    [Display(Name = "Discipline")]
    public int? DisciplineId { get; set; }

    [Display(Name = "Position / Title")]
    public string? Position { get; set; }

    public List<(int Id, string Name)> AllEmployees { get; set; } = new();
    public List<(int Id, string Name)> AllDepartments { get; set; } = new();
    public List<(int Id, string Name)> AllDisciplines { get; set; } = new();

    public bool IsEdit => Id > 0;
}
