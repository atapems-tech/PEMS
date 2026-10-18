using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Dynamic Maintenance Organization» - پیاده‌سازی روی همان Entity های موجود
/// (MaintenanceGroup/Team/Position + EmployeeAssignment تازه)، هم‌الگوی دقیق TeamService
/// (پیام خطای فارسی، Exception ساده به‌جای نوع اختصاصی، تراکنش با SaveChangesAsync معمولی).
/// </summary>
public class OrganizationStructureService : IOrganizationStructureService
{
    private readonly PEMSDbContext _context;

    public OrganizationStructureService(PEMSDbContext context)
    {
        _context = context;
    }

    // ============================== Maintenance Group ==============================

    public async Task<int> CreateGroupAsync(string code, string name, int disciplineId, int? parentMaintenanceGroupId, string? description)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            throw new Exception("کد و نام گروه تعمیراتی الزامی هستند.");

        if (!await _context.Disciplines.AnyAsync(x => x.DisciplineId == disciplineId))
            throw new Exception("دیسیپلین یافت نشد.");

        if (parentMaintenanceGroupId.HasValue &&
            !await _context.MaintenanceGroups.AnyAsync(x => x.MaintenanceGroupId == parentMaintenanceGroupId.Value))
            throw new Exception("گروه والد یافت نشد.");

        var duplicate = await _context.MaintenanceGroups
            .AnyAsync(x => x.Name == name.Trim() || x.Code == code.Trim());
        if (duplicate)
            throw new Exception("گروه تعمیراتی با همین کد یا نام از قبل وجود دارد.");

        var group = new MaintenanceGroup
        {
            Code = code.Trim(),
            Name = name.Trim(),
            DisciplineId = disciplineId,
            ParentMaintenanceGroupId = parentMaintenanceGroupId,
            Description = description,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.MaintenanceGroups.AddAsync(group);
        await _context.SaveChangesAsync();
        return group.MaintenanceGroupId;
    }

    public async Task MoveGroupAsync(int maintenanceGroupId, int? newParentMaintenanceGroupId)
    {
        var group = await _context.MaintenanceGroups.FirstOrDefaultAsync(x => x.MaintenanceGroupId == maintenanceGroupId);
        if (group == null) throw new Exception("گروه تعمیراتی یافت نشد.");

        if (newParentMaintenanceGroupId.HasValue)
        {
            if (newParentMaintenanceGroupId.Value == maintenanceGroupId)
                throw new Exception("یک گروه نمی‌تواند والد خودش باشد.");

            var newParent = await _context.MaintenanceGroups
                .FirstOrDefaultAsync(x => x.MaintenanceGroupId == newParentMaintenanceGroupId.Value);
            if (newParent == null) throw new Exception("گروه والد جدید یافت نشد.");

            if (await IsDescendantOfAsync(candidateAncestorId: maintenanceGroupId, nodeId: newParentMaintenanceGroupId.Value,
                    getParentId: async id => (await _context.MaintenanceGroups.Where(x => x.MaintenanceGroupId == id)
                        .Select(x => (int?)x.ParentMaintenanceGroupId).FirstOrDefaultAsync())))
            {
                throw new Exception("این جابه‌جایی یک سلسله‌مراتب چرخه‌ای می‌سازد (گروه مقصد زیرمجموعه‌ی همین گروه است).");
            }
        }

        group.ParentMaintenanceGroupId = newParentMaintenanceGroupId;
        await _context.SaveChangesAsync();
    }

    public async Task DeactivateGroupAsync(int maintenanceGroupId)
    {
        var group = await _context.MaintenanceGroups.FirstOrDefaultAsync(x => x.MaintenanceGroupId == maintenanceGroupId);
        if (group == null) throw new Exception("گروه تعمیراتی یافت نشد.");

        // طبق بند ۲۲: فقط خودِ گره غیرفعال می‌شود - زیرگروه‌ها/تیم‌های زیرمجموعه دست‌نخورده
        // می‌مانند (هم‌رفتار با ToggleGroupActive موجود؛ حذف آبشاری اختراع نشد).
        group.IsActive = false;
        await _context.SaveChangesAsync();
    }

    // ============================== Team ==============================

    public async Task<int> CreateTeamNodeAsync(string code, string name, int maintenanceGroupId, int? parentTeamId,
        string nodeKind, int? supervisorEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            throw new Exception("کد و نام تیم الزامی هستند.");

        var group = await _context.MaintenanceGroups.FirstOrDefaultAsync(x => x.MaintenanceGroupId == maintenanceGroupId);
        if (group == null) throw new Exception("گروه تعمیراتی یافت نشد.");

        if (parentTeamId.HasValue)
        {
            var parentTeam = await _context.Teams.FirstOrDefaultAsync(x => x.TeamId == parentTeamId.Value);
            if (parentTeam == null) throw new Exception("تیم والد یافت نشد.");
            if (parentTeam.MaintenanceGroupId != maintenanceGroupId)
                throw new Exception("تیم والد متعلق به گروه تعمیراتی دیگری است.");
        }

        if (supervisorEmployeeId.HasValue &&
            !await _context.Employees.AnyAsync(x => x.EmployeeId == supervisorEmployeeId.Value))
            throw new Exception("کارمند سرپرست یافت نشد.");

        var duplicate = await _context.Teams.AnyAsync(x => x.Name == name.Trim() || x.Code == code.Trim());
        if (duplicate)
            throw new Exception("تیمی با همین کد یا نام از قبل وجود دارد.");

        var team = new Team
        {
            Code = code.Trim(),
            Name = name.Trim(),
            MaintenanceGroupId = maintenanceGroupId,
            ParentTeamId = parentTeamId,
            NodeKind = string.IsNullOrWhiteSpace(nodeKind) ? "Team" : nodeKind.Trim(),
            SupervisorEmployeeId = supervisorEmployeeId,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.Teams.AddAsync(team);
        await _context.SaveChangesAsync();
        return team.TeamId;
    }

    public async Task MoveTeamNodeAsync(int teamId, int? newParentTeamId, int? newMaintenanceGroupId)
    {
        var team = await _context.Teams.FirstOrDefaultAsync(x => x.TeamId == teamId);
        if (team == null) throw new Exception("تیم یافت نشد.");

        var targetGroupId = newMaintenanceGroupId ?? team.MaintenanceGroupId;

        if (newParentTeamId.HasValue)
        {
            if (newParentTeamId.Value == teamId)
                throw new Exception("یک تیم نمی‌تواند والد خودش باشد.");

            var newParent = await _context.Teams.FirstOrDefaultAsync(x => x.TeamId == newParentTeamId.Value);
            if (newParent == null) throw new Exception("تیم والد جدید یافت نشد.");

            // طبق طراحی CreateTeamNodeAsync: تیم همیشه در همان گروه تعمیراتی والدش قرار می‌گیرد.
            targetGroupId = newParent.MaintenanceGroupId;
            if (newMaintenanceGroupId.HasValue && newMaintenanceGroupId.Value != targetGroupId)
                throw new Exception("گروه تعمیراتی داده‌شده با گروه تیم والد جدید سازگار نیست.");

            if (await IsDescendantOfAsync(candidateAncestorId: teamId, nodeId: newParentTeamId.Value,
                    getParentId: async id => (await _context.Teams.Where(x => x.TeamId == id)
                        .Select(x => (int?)x.ParentTeamId).FirstOrDefaultAsync())))
            {
                throw new Exception("این جابه‌جایی یک سلسله‌مراتب چرخه‌ای می‌سازد (تیم مقصد زیرمجموعه‌ی همین تیم است).");
            }
        }
        else if (newMaintenanceGroupId.HasValue &&
                 !await _context.MaintenanceGroups.AnyAsync(x => x.MaintenanceGroupId == newMaintenanceGroupId.Value))
        {
            throw new Exception("گروه تعمیراتی مقصد یافت نشد.");
        }

        team.ParentTeamId = newParentTeamId;
        team.MaintenanceGroupId = targetGroupId;
        await _context.SaveChangesAsync();
    }

    public async Task DeactivateTeamNodeAsync(int teamId)
    {
        var team = await _context.Teams.FirstOrDefaultAsync(x => x.TeamId == teamId);
        if (team == null) throw new Exception("تیم یافت نشد.");

        team.IsActive = false;
        await _context.SaveChangesAsync();
    }

    // ============================== Position ==============================

    public async Task<int> CreatePositionAsync(int? teamId, int? departmentId, string title, string? description, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new Exception("عنوان سمت الزامی است.");

        if (teamId.HasValue && !await _context.Teams.AnyAsync(x => x.TeamId == teamId.Value))
            throw new Exception("تیم یافت نشد.");

        if (departmentId.HasValue && !await _context.Departments.AnyAsync(x => x.DepartmentId == departmentId.Value))
            throw new Exception("دپارتمان یافت نشد.");

        var position = new Position
        {
            Title = title.Trim(),
            Description = description,
            TeamId = teamId,
            DepartmentId = departmentId,
            SortOrder = sortOrder,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.Positions.AddAsync(position);
        await _context.SaveChangesAsync();
        return position.PositionId;
    }

    // طبق «PEMS Dynamic Maintenance Organization — Phase 6C» بند ۷: ویرایش افزایشی روی همان
    // Entity موجود (Title/Description/SortOrder) - نه یک معماری مدیریت سمت دوم؛ فیلد/جدول تازه‌ای
    // اضافه نشده.
    public async Task UpdatePositionAsync(int positionId, string title, string? description, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new Exception("عنوان سمت الزامی است.");

        var position = await _context.Positions.FirstOrDefaultAsync(x => x.PositionId == positionId);
        if (position == null) throw new Exception("سمت یافت نشد.");

        position.Title = title.Trim();
        position.Description = description;
        position.SortOrder = sortOrder;
        await _context.SaveChangesAsync();
    }

    public async Task MovePositionAsync(int positionId, int? newTeamId)
    {
        var position = await _context.Positions.FirstOrDefaultAsync(x => x.PositionId == positionId);
        if (position == null) throw new Exception("سمت یافت نشد.");

        if (newTeamId.HasValue && !await _context.Teams.AnyAsync(x => x.TeamId == newTeamId.Value))
            throw new Exception("تیم مقصد یافت نشد.");

        position.TeamId = newTeamId;
        await _context.SaveChangesAsync();
    }

    public async Task DeactivatePositionAsync(int positionId, string? notes)
    {
        var position = await _context.Positions.FirstOrDefaultAsync(x => x.PositionId == positionId);
        if (position == null) throw new Exception("سمت یافت نشد.");

        position.IsActive = false;

        // طبق بند ۱۸: سمت غیرفعال نباید تخصیص فعال نشان دهد - تخصیص جاری (اگر باشد) بسته
        // می‌شود؛ خودِ رکورد حذف نمی‌شود (تاریخچه حفظ می‌شود).
        var activeAssignment = await _context.EmployeeAssignments
            .FirstOrDefaultAsync(x => x.PositionId == positionId && x.IsActive);
        if (activeAssignment != null)
        {
            activeAssignment.IsActive = false;
            activeAssignment.EndDate = DateTime.Now;
            activeAssignment.Notes = notes ?? activeAssignment.Notes;
        }

        await _context.SaveChangesAsync();
    }

    // ============================== Employee Assignment ==============================

    public async Task<int> AssignEmployeeAsync(int positionId, int employeeId, string? notes)
    {
        var position = await _context.Positions.FirstOrDefaultAsync(x => x.PositionId == positionId);
        if (position == null) throw new Exception("سمت یافت نشد.");
        if (!position.IsActive) throw new Exception("این سمت غیرفعال است و نمی‌تواند تخصیص جدید بگیرد.");

        var employee = await _context.Employees.FirstOrDefaultAsync(x => x.EmployeeId == employeeId);
        if (employee == null) throw new Exception("کارمند یافت نشد.");
        if (!employee.IsActive) throw new Exception("این کارمند غیرفعال است و نمی‌تواند به سمتی تخصیص یابد.");

        var alreadyOccupied = await _context.EmployeeAssignments
            .AnyAsync(x => x.PositionId == positionId && x.IsActive);
        if (alreadyOccupied)
            throw new Exception("این سمت هم‌اکنون اشغال شده است؛ ابتدا تخصیص فعلی را پایان دهید یا از جابه‌جایی استفاده کنید.");

        // سمت اصلی قطعی: اولین تخصیص فعالِ کارمند اصلی است؛ تخصیص‌های بعدی Secondary (مدیر مستقیم را تعیین نمی‌کنند).
        var hasPrimary = await _context.EmployeeAssignments.AnyAsync(x => x.EmployeeId == employeeId && x.IsActive && x.IsPrimary);

        var assignment = new EmployeeAssignment
        {
            PositionId = positionId,
            EmployeeId = employeeId,
            StartDate = DateTime.Now,
            IsActive = true,
            IsPrimary = !hasPrimary,
            AssignmentType = hasPrimary ? EmployeeAssignmentTypes.Secondary : EmployeeAssignmentTypes.Primary,
            Notes = notes,
            CreatedDate = DateTime.Now
        };

        await _context.EmployeeAssignments.AddAsync(assignment);
        await _context.SaveChangesAsync();
        return assignment.EmployeeAssignmentId;
    }

    public async Task RemoveAssignmentAsync(int positionId, string? notes)
    {
        var assignment = await _context.EmployeeAssignments
            .FirstOrDefaultAsync(x => x.PositionId == positionId && x.IsActive);
        if (assignment == null) throw new Exception("این سمت هم‌اکنون خالی (Vacant) است.");

        assignment.IsActive = false;
        assignment.EndDate = DateTime.Now;
        assignment.Notes = notes ?? assignment.Notes;

        await _context.SaveChangesAsync();
    }

    public async Task MoveEmployeeAsync(int fromPositionId, int toPositionId, string? notes)
    {
        var currentAssignment = await _context.EmployeeAssignments
            .FirstOrDefaultAsync(x => x.PositionId == fromPositionId && x.IsActive);
        if (currentAssignment == null)
            throw new Exception("سمت مبدأ هم‌اکنون خالی است؛ جابه‌جایی ممکن نیست.");

        var employeeId = currentAssignment.EmployeeId;

        currentAssignment.IsActive = false;
        currentAssignment.EndDate = DateTime.Now;
        currentAssignment.Notes = notes ?? currentAssignment.Notes;
        await _context.SaveChangesAsync();

        await AssignEmployeeAsync(toPositionId, employeeId, notes);
    }

    // ============================== Read models ==============================

    public async Task<List<OrganizationGroupNodeDto>> GetOrganizationTreeAsync(int? disciplineId = null)
    {
        var groupsQuery = _context.MaintenanceGroups.Include(x => x.Discipline).AsQueryable();
        if (disciplineId.HasValue) groupsQuery = groupsQuery.Where(x => x.DisciplineId == disciplineId.Value);
        var groups = await groupsQuery.ToListAsync();

        var groupIds = groups.Select(x => x.MaintenanceGroupId).ToHashSet();
        var teams = await _context.Teams
            .Where(x => groupIds.Contains(x.MaintenanceGroupId))
            .Include(x => x.SupervisorEmployee)
            .Include(x => x.Members)
            .ToListAsync();

        var teamIds = teams.Select(x => x.TeamId).ToHashSet();
        var positions = await _context.Positions
            .Where(x => x.TeamId != null && teamIds.Contains(x.TeamId!.Value))
            .Include(x => x.Assignments).ThenInclude(a => a.Employee)
            .ToListAsync();

        var groupById = groups.ToDictionary(g => g.MaintenanceGroupId);
        var teamDtoById = teams.ToDictionary(t => t.TeamId, t =>
        {
            var g = groupById.TryGetValue(t.MaintenanceGroupId, out var gg) ? gg : null;
            return BuildTeamDto(t, positions.Where(p => p.TeamId == t.TeamId), g?.DisciplineId ?? 0, g?.Discipline?.Name);
        });

        // درخت تیم‌ها (ParentTeamId)
        foreach (var t in teams)
        {
            if (t.ParentTeamId.HasValue && teamDtoById.TryGetValue(t.ParentTeamId.Value, out var parentDto))
                parentDto.Children.Add(teamDtoById[t.TeamId]);
        }

        var groupDtoById = groups.ToDictionary(g => g.MaintenanceGroupId, g => new OrganizationGroupNodeDto
        {
            MaintenanceGroupId = g.MaintenanceGroupId,
            Code = g.Code,
            Name = g.Name,
            Description = g.Description,
            IsActive = g.IsActive,
            DisciplineId = g.DisciplineId,
            DisciplineName = g.Discipline?.Name,
            ParentMaintenanceGroupId = g.ParentMaintenanceGroupId
        });

        // تیم‌های ریشه (بدون ParentTeamId) زیر گروه خودشان
        foreach (var t in teams.Where(x => x.ParentTeamId == null))
        {
            if (groupDtoById.TryGetValue(t.MaintenanceGroupId, out var groupDto))
                groupDto.Teams.Add(teamDtoById[t.TeamId]);
        }

        // درخت گروه‌ها (ParentMaintenanceGroupId)
        var roots = new List<OrganizationGroupNodeDto>();
        foreach (var g in groups)
        {
            var dto = groupDtoById[g.MaintenanceGroupId];
            if (g.ParentMaintenanceGroupId.HasValue && groupDtoById.TryGetValue(g.ParentMaintenanceGroupId.Value, out var parentDto))
                parentDto.Children.Add(dto);
            else
                roots.Add(dto);
        }

        return roots.OrderBy(x => x.Name).ToList();
    }

    public async Task<OrganizationTeamNodeDto?> GetTeamDetailsAsync(int teamId)
    {
        var team = await _context.Teams
            .Include(x => x.SupervisorEmployee)
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.TeamId == teamId);
        if (team == null) return null;

        var descendantIds = await GetDescendantTeamIdsAsync(teamId);
        descendantIds.Add(teamId);

        var teams = await _context.Teams
            .Where(x => descendantIds.Contains(x.TeamId))
            .Include(x => x.SupervisorEmployee)
            .Include(x => x.Members)
            // طبق بند ۱۳ فاز 6C: دیسیپلین گره از همان MaintenanceGroup موجود، بدون Query تازه
            .Include(x => x.MaintenanceGroup).ThenInclude(g => g!.Discipline)
            .ToListAsync();

        var positions = await _context.Positions
            .Where(x => x.TeamId != null && descendantIds.Contains(x.TeamId!.Value))
            .Include(x => x.Assignments).ThenInclude(a => a.Employee)
            .ToListAsync();

        var dtoById = teams.ToDictionary(t => t.TeamId, t =>
            BuildTeamDto(t, positions.Where(p => p.TeamId == t.TeamId), t.MaintenanceGroup?.DisciplineId ?? 0, t.MaintenanceGroup?.Discipline?.Name));

        foreach (var t in teams.Where(x => x.TeamId != teamId))
        {
            if (t.ParentTeamId.HasValue && dtoById.TryGetValue(t.ParentTeamId.Value, out var parentDto))
                parentDto.Children.Add(dtoById[t.TeamId]);
        }

        return dtoById[teamId];
    }

    // ============================== Helpers ==============================

    private static OrganizationTeamNodeDto BuildTeamDto(Team team, IEnumerable<Position> positionsOfTeam, int disciplineId, string? disciplineName)
    {
        var positionDtos = positionsOfTeam
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Title)
            .Select(p =>
            {
                var active = p.Assignments.FirstOrDefault(a => a.IsActive);

                EmployeeAssignmentDto ToDto(EmployeeAssignment a) => new()
                {
                    EmployeeAssignmentId = a.EmployeeAssignmentId,
                    PositionId = a.PositionId,
                    EmployeeId = a.EmployeeId,
                    EmployeeName = a.Employee == null ? "" : $"{a.Employee.FirstName} {a.Employee.LastName}",
                    StartDate = a.StartDate,
                    EndDate = a.EndDate,
                    IsActive = a.IsActive,
                    Notes = a.Notes
                };

                return new OrganizationPositionDto
                {
                    PositionId = p.PositionId,
                    Title = p.Title,
                    Description = p.Description,
                    SortOrder = p.SortOrder,
                    IsActive = p.IsActive,
                    TeamId = p.TeamId,
                    DepartmentId = p.DepartmentId,
                    CurrentAssignment = active == null ? null : ToDto(active),
                    // طبق بند ۱۷: تاریخچه‌ی کامل (فعال + پایان‌یافته)، نزولی بر اساس StartDate
                    History = p.Assignments.OrderByDescending(a => a.StartDate).Select(ToDto).ToList()
                };
            })
            .ToList();

        return new OrganizationTeamNodeDto
        {
            TeamId = team.TeamId,
            Code = team.Code,
            Name = team.Name,
            NodeKind = team.NodeKind,
            IsActive = team.IsActive,
            MaintenanceGroupId = team.MaintenanceGroupId,
            ParentTeamId = team.ParentTeamId,
            DisciplineId = disciplineId,
            DisciplineName = disciplineName,
            SupervisorEmployeeId = team.SupervisorEmployeeId,
            SupervisorName = team.SupervisorEmployee == null ? null : $"{team.SupervisorEmployee.FirstName} {team.SupervisorEmployee.LastName}",
            Positions = positionDtos,
            MemberCount = team.Members.Count(m => m.IsActive),
            PositionCount = positionDtos.Count,
            VacantPositionCount = positionDtos.Count(p => p.IsVacant && p.IsActive)
        };
    }

    private async Task<HashSet<int>> GetDescendantTeamIdsAsync(int teamId)
    {
        // مجموعه‌ی داده امروز کوچک است (بند ۳۴: بارگذاری کامل هنوز قابل‌قبول)؛ رشد به هزاران
        // گره نیاز به صفحه‌بندی/محدودسازی سطح خواهد داشت - این نسخه آن را حل نمی‌کند (مستند در
        // گزارش نهایی).
        var all = await _context.Teams.Select(x => new { x.TeamId, x.ParentTeamId }).ToListAsync();
        var result = new HashSet<int>();
        var frontier = new Queue<int>();
        frontier.Enqueue(teamId);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var child in all.Where(x => x.ParentTeamId == current))
            {
                if (result.Add(child.TeamId))
                    frontier.Enqueue(child.TeamId);
            }
        }

        return result;
    }

    // چرخه را با بالارفتن از nodeId از طریق زنجیره‌ی والد بررسی می‌کند - اگر در مسیر به
    // candidateAncestorId برسیم، یعنی nodeId زیرمجموعه‌ی candidateAncestorId است (و پس نمی‌تواند
    // والد جدید آن باشد). محدود به تعداد کل گره‌ها تا داده‌ی خراب حلقه‌ی بی‌نهایت نسازد.
    private static async Task<bool> IsDescendantOfAsync(int candidateAncestorId, int nodeId, Func<int, Task<int?>> getParentId)
    {
        var current = (int?)nodeId;
        var guard = 0;
        while (current.HasValue && guard++ < 100000)
        {
            if (current.Value == candidateAncestorId) return true;
            current = await getParentId(current.Value);
        }
        return false;
    }
}
