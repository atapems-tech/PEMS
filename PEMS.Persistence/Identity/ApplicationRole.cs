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

    // متادیتای Role Catalog (افزودنی). Name همچنان کلید پایدار و بی‌زبان است؛ Domain/Category
    // فقط توصیف مسئولیت‌اند (دامنه‌ی سازمانی/دیسیپلین «Scope» است و در نقش کدگذاری نمی‌شود).
    public string? Domain { get; set; }

    // Platform | Domain | Functional | ReadOnly
    public string? Category { get; set; }

    // نقش پایه‌ی کاتالوگ - حذف‌ناپذیر (جایگزین بررسی صرفِ SystemRoles.All)
    public bool IsSystemRole { get; set; }

    public int SortOrder { get; set; }
}
