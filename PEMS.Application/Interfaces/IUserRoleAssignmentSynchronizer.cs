namespace PEMS.Application.Interfaces;

/// <summary>
/// هم‌ترازسازی «آینه‌ی» نقش‌های Identity (AspNetUserRoles) با RoleAssignments. AccessControlService اجتماع هر دو را زنده می‌خواند؛
/// بدون این هم‌ترازی، حذف نقش از AspNetUserRoles یک RoleAssignment فعالِ Direct/Global را باقی می‌گذارد و نقشِ حذف‌شده همچنان دسترسی می‌دهد.
/// فقط تخصیص‌های «آینه‌ای» (Direct + Global + بدون تاریخ + بدون Scope) را لمس می‌کند؛ Scoped/Delegated/Emergency/Temporary دست‌نخورده می‌مانند.
/// </summary>
public interface IUserRoleAssignmentSynchronizer
{
    /// <summary>برای هر نقش فعلیِ Identity یک آینه‌ی فعال تضمین می‌شود و آینه‌های نقش‌های حذف‌شده غیرفعال (نه حذف) می‌شوند. تعداد تغییرها را برمی‌گرداند.</summary>
    Task<int> SyncAsync(int userId, CancellationToken ct = default);

    /// <summary>آینه‌های فعالی که نقش Identity متناظرشان ندارد (برای تشخیص/گزارش؛ تغییری نمی‌دهد).</summary>
    Task<IReadOnlyList<int>> FindStaleMirrorsAsync(CancellationToken ct = default);
}
