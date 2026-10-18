using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Dynamic Maintenance Organization»: عملیات پویا روی سلسله‌مراتب موجود
/// (MaintenanceGroup تودرتو، Team تودرتو، Position، تخصیص/خروج/جابه‌جایی Employee) - هیچ
/// سیستم سازمانی موازی نمی‌سازد؛ روی همان Entity های موجود (MaintenanceGroup/Team/Position)
/// کار می‌کند. ITeamService/CrewService موجود دست‌نخورده می‌مانند (عضویت TeamMember/CrewMember
/// از طریق MaintenanceRole - مفهومی جدا از Position/EmployeeAssignment این‌جا؛ بخش ۳-۴ سند).
/// </summary>
public interface IOrganizationStructureService
{
    // --- Maintenance Group (تودرتویی دلخواه) ---

    Task<int> CreateGroupAsync(string code, string name, int disciplineId, int? parentMaintenanceGroupId, string? description);

    // newParentMaintenanceGroupId = null یعنی انتقال به ریشه. جلوگیری از چرخه (والد نمی‌تواند
    // فرزند/نوه‌ی خودش شود) و خودارجاعی.
    Task MoveGroupAsync(int maintenanceGroupId, int? newParentMaintenanceGroupId);

    Task DeactivateGroupAsync(int maintenanceGroupId);

    // --- Team (تودرتویی دلخواه: زیرتیم، شیفت به‌عنوان گره، واحد عملیاتی) ---

    Task<int> CreateTeamNodeAsync(string code, string name, int maintenanceGroupId, int? parentTeamId,
        string nodeKind, int? supervisorEmployeeId);

    // newMaintenanceGroupId=null یعنی گروه فعلی حفظ شود؛ newParentTeamId صراحتاً هر مقدار
    // (از جمله null برای تبدیل به تیم ریشه) اعمال می‌شود.
    Task MoveTeamNodeAsync(int teamId, int? newParentTeamId, int? newMaintenanceGroupId);

    Task DeactivateTeamNodeAsync(int teamId);

    // --- Position (سمت سازمانی - جدا از Employee) ---

    Task<int> CreatePositionAsync(int? teamId, int? departmentId, string title, string? description, int sortOrder);

    // طبق «PEMS Dynamic Maintenance Organization — Phase 6C» بند ۷: ویرایش افزایشی روی همان
    // Entity - عنوان/توضیحات/ترتیب نمایش؛ TeamId از این مسیر تغییر نمی‌کند (آن کار MovePositionAsync است).
    Task UpdatePositionAsync(int positionId, string title, string? description, int sortOrder);

    Task MovePositionAsync(int positionId, int? newTeamId);

    // طبق بند ۲۲: غیرفعال‌سازی سمت - سمت حذف نمی‌شود؛ تخصیص فعال آن (اگر باشد) هم بسته می‌شود
    // (بند ۱۸، تاریخچه حفظ می‌شود).
    Task DeactivatePositionAsync(int positionId, string? notes);

    // --- Employee Assignment (اشغال/تخلیه/جابه‌جایی سمت) ---

    // اگر Position از قبل اشغال‌شده باشد رد می‌شود (باید ابتدا RemoveAssignment یا MoveEmployee
    // فراخوانی شود) - رونویسی خاموش مجاز نیست.
    Task<int> AssignEmployeeAsync(int positionId, int employeeId, string? notes);

    // پایان تخصیص فعال = EndDate/IsActive=false (بند ۱۸)؛ Position به VACANT برمی‌گردد، حذف
    // نمی‌شود.
    Task RemoveAssignmentAsync(int positionId, string? notes);

    // خروج از Position مبدأ (اگر تخصیص فعالی داشته باشد) + اشغال Position مقصد - یک تراکنش.
    Task MoveEmployeeAsync(int fromPositionId, int toPositionId, string? notes);

    // --- Read models ---

    Task<List<OrganizationGroupNodeDto>> GetOrganizationTreeAsync(int? disciplineId = null);

    Task<OrganizationTeamNodeDto?> GetTeamDetailsAsync(int teamId);
}
