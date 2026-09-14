using PEMS.Application.Interfaces;

namespace PEMS.Tests.TestSupport;

/// <summary>
/// پیاده‌سازی ساده‌ی ICurrentUserContext (نه ICurrentUserService - آن یکی PEMS.Web است، این
/// یکی برای Providerهای جستجو در PEMS.Persistence استفاده می‌شود) - طبق «PEMS Phase 2A —
/// Global Search Security» برای تست Provider هایی که حالا به مجوز/نقش/کارمند کاربر جاری
/// نیاز دارند.
/// </summary>
public class FakeCurrentUserContext : ICurrentUserContext
{
    public int? UserId { get; set; }
    public int? EmployeeId { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = new List<string>();
}
