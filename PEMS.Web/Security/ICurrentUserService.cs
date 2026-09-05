namespace PEMS.Web.Security;

/// <summary>
/// دسترسی ساده به اطلاعات کاربر جاری (بدون نیاز به تزریق HttpContext در همه‌جا).
/// </summary>
public interface ICurrentUserService
{
    int? UserId { get; }

    string? UserName { get; }

    string? FullName { get; }

    bool IsAuthenticated { get; }

    IReadOnlyList<string> Roles { get; }
}
