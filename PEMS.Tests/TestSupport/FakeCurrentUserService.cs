using PEMS.Web.Security;

namespace PEMS.Tests.TestSupport;

/// <summary>
/// پیاده‌سازی ساده‌ی ICurrentUserService برای تست کنترلرهایی که [RequirePermission]/
/// HasPermissionAsync دارند (WorkOrder، WorkRequest، MaterialRequisition، ...) - طبق فاز ۶B
/// Wave A، این کنترلرها باید بدون شکستن Authorization موجود تست شوند.
/// </summary>
public class FakeCurrentUserService : ICurrentUserService
{
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string? FullName { get; set; }
    public bool IsAuthenticated { get; set; } = true;
    public IReadOnlyList<string> Roles { get; set; } = new List<string>();
}
