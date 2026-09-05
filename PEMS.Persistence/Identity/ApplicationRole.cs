using Microsoft.AspNetCore.Identity;

namespace PEMS.Persistence.Identity;

/// <summary>
/// نقش سیستم بر پایه‌ی ASP.NET Core Identity (جدول AspNetRoles).
/// </summary>
public class ApplicationRole : IdentityRole<int>
{
    public ApplicationRole() { }

    public ApplicationRole(string roleName) : base(roleName) { }

    // نام قابل‌نمایش فارسی/توضیحی نقش (Name خود Identity باید بدون فاصله/انگلیسی باشد)
    public string? DisplayName { get; set; }

    public string? Description { get; set; }
}
