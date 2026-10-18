namespace PEMS.Application.Interfaces;

public sealed record DisciplineRow(int DisciplineId, string Code, string Name, string? Description, string? ImportAliases, string? TransmittalCode,
    bool IsActive, int EmployeeCount, int DocumentCount);

public sealed record DisciplineOpResult(bool Success, string? ErrorKey = null)
{
    public static DisciplineOpResult Ok() => new(true);
    public static DisciplineOpResult Fail(string key) => new(false, key);
}

/// <summary>
/// Master-data مدیریت دیسیپلین روی همان Entity موجود (Discipline). دیسیپلین «زمینه‌ی حرفه‌ای» است، نه واحد سازمانی و نه نقش مجوز.
/// حذف فیزیکی وجود ندارد (FK از Employee/Document/Team/...)؛ فقط غیرفعال/فعال‌سازی. Code و Name یکتا؛ Aliasها نباید با Code/Name/Alias دیسیپلین دیگر تداخل کنند (Import بدون ابهام).
/// </summary>
public interface IDisciplineMasterService
{
    Task<List<DisciplineRow>> GetAllAsync(CancellationToken ct = default);
    Task<DisciplineOpResult> CreateAsync(string code, string name, string? description, string? importAliases, string? transmittalCode, CancellationToken ct = default);
    Task<DisciplineOpResult> UpdateAsync(int disciplineId, string code, string name, string? description, string? importAliases, string? transmittalCode, CancellationToken ct = default);
    Task<DisciplineOpResult> SetActiveAsync(int disciplineId, bool isActive, CancellationToken ct = default);
}
