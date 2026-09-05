using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class TeamService : ITeamService
{
    private readonly PEMSDbContext _context;

    public TeamService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task AddOrReactivateMemberAsync(int teamId, int employeeId, int roleId)
    {
        var team = await _context.Teams.FirstOrDefaultAsync(x => x.TeamId == teamId);
        if (team == null) throw new Exception("تیم یافت نشد.");

        // Rule 3: تیم غیرفعال نباید Employee جدید دریافت کند
        if (!team.IsActive)
            throw new Exception("این تیم غیرفعال است و نمی‌تواند عضو جدید بگیرد.");

        var employee = await _context.Employees.FirstOrDefaultAsync(x => x.EmployeeId == employeeId);
        if (employee == null) throw new Exception("کارمند یافت نشد.");

        // Rule 1: کارمند غیرفعال نباید به تیم جدید اضافه شود
        if (!employee.IsActive)
            throw new Exception("این کارمند غیرفعال است و نمی‌تواند به تیم جدید اضافه شود.");

        var existing = await _context.TeamMembers
            .FirstOrDefaultAsync(x => x.TeamId == teamId && x.EmployeeId == employeeId);

        if (existing != null && existing.IsActive)
        {
            // حالت ۱: عضویت فعال از قبل هست
            throw new Exception("این کارمند از قبل عضو فعال این تیم است.");
        }

        if (existing != null && !existing.IsActive)
        {
            // حالت ۲: عضویت قبلی غیرفعال است - Reactivate، نه رکورد جدید
            existing.IsActive = true;
            existing.LeftDate = null;
            existing.JoinedDate = DateTime.Now;
            existing.RoleId = roleId;

            await _context.SaveChangesAsync();
            return;
        }

        // حالت ۳: هیچ عضویتی نیست - رکورد جدید
        await _context.TeamMembers.AddAsync(new TeamMember
        {
            TeamId = teamId,
            EmployeeId = employeeId,
            RoleId = roleId,
            JoinedDate = DateTime.Now,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
    }

    public async Task RemoveMemberAsync(int teamMemberId)
    {
        var member = await _context.TeamMembers.FirstOrDefaultAsync(x => x.TeamMemberId == teamMemberId);
        if (member == null) throw new Exception("عضویت یافت نشد.");

        member.IsActive = false;
        member.LeftDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    public async Task MoveMemberAsync(int employeeId, int fromTeamId, int toTeamId, int roleId)
    {
        var currentMembership = await _context.TeamMembers
            .FirstOrDefaultAsync(x => x.TeamId == fromTeamId && x.EmployeeId == employeeId && x.IsActive);

        if (currentMembership != null)
        {
            currentMembership.IsActive = false;
            currentMembership.LeftDate = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        await AddOrReactivateMemberAsync(toTeamId, employeeId, roleId);
    }
}
