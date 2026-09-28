namespace PEMS.Application.DTOs;

// طبق «PEMS Dynamic Maintenance Organization»: مدل خواندنیِ درخت سازمانی (Discipline ⊃
// MaintenanceGroup تودرتو ⊃ Team تودرتو ⊃ Position ⊃ تخصیص/خالی) - مصرف‌کننده‌ی این DTO ها
// می‌تواند صفحه‌ی جزئیات تیم امروز یا یک نمودار سازمانی آینده باشد؛ خودِ این فاز چیزی بیش از
// این مدل خواندنی و سرویس پشت آن نمی‌سازد (بدون کامپوننت گرافیکی نمودار - فاز جداگانه).

public class OrganizationGroupNodeDto
{
    public int MaintenanceGroupId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int DisciplineId { get; set; }
    public string? DisciplineName { get; set; }
    public int? ParentMaintenanceGroupId { get; set; }

    public List<OrganizationGroupNodeDto> Children { get; set; } = new();
    public List<OrganizationTeamNodeDto> Teams { get; set; } = new();
}

public class OrganizationTeamNodeDto
{
    public int TeamId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NodeKind { get; set; } = "Team";
    public bool IsActive { get; set; }
    public int MaintenanceGroupId { get; set; }
    public int? ParentTeamId { get; set; }
    public int? SupervisorEmployeeId { get; set; }
    public string? SupervisorName { get; set; }

    // طبق «PEMS Dynamic Maintenance Organization — Phase 6C» بند ۱۳: کاربر باید همیشه دیسیپلین
    // گره را بداند (بخشی از «زمینه‌ی سازمانی») - از همان MaintenanceGroup.Discipline موجود
    // پر می‌شود، مفهوم/فیلد تازه‌ای نیست.
    public int DisciplineId { get; set; }
    public string? DisciplineName { get; set; }

    public List<OrganizationTeamNodeDto> Children { get; set; } = new();
    public List<OrganizationPositionDto> Positions { get; set; } = new();

    public int MemberCount { get; set; }
    public int PositionCount { get; set; }
    public int VacantPositionCount { get; set; }
}

public class OrganizationPositionDto
{
    public int PositionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public int? TeamId { get; set; }
    public int? DepartmentId { get; set; }

    public bool IsVacant => CurrentAssignment == null;
    public EmployeeAssignmentDto? CurrentAssignment { get; set; }

    // طبق «PEMS Dynamic Maintenance Organization — Phase 6B» بند ۱۷: تاریخچه‌ی کامل تخصیص‌ها
    // (فعال + پایان‌یافته)، نزولی بر اساس StartDate. از همان Assignments که سرویس قبلاً با
    // Include می‌خواند پر می‌شود - کوئری تازه‌ای اضافه نمی‌کند.
    public List<EmployeeAssignmentDto> History { get; set; } = new();
}

public class EmployeeAssignmentDto
{
    public int EmployeeAssignmentId { get; set; }
    public int PositionId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}
