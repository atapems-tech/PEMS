using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class ProjectService : IProjectService
{
    private readonly PEMSDbContext _context;

    public ProjectService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProjectDto>> GetAllAsync()
    {
        return await _context.Projects
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new ProjectDto
            {
                ProjectId = x.ProjectId,
                ProjectCode = x.ProjectCode,
                ProjectName = x.ProjectName,
                ClientName = x.ClientName,
                Location = x.Location,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                Status = x.Status,
                IsActive = x.IsActive,
                CreatedDate = x.CreatedDate,
                DocumentsCount = _context.EngineeringDocuments.Count(d => d.ProjectId == x.ProjectId)
            })
            .ToListAsync();
    }

    public async Task<Project?> GetEntityByIdAsync(int id)
    {
        return await _context.Projects.FirstOrDefaultAsync(x => x.ProjectId == id);
    }

    public async Task AddAsync(Project project)
    {
        project.CreatedDate = DateTime.Now;
        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Project project)
    {
        _context.Projects.Update(project);
        await _context.SaveChangesAsync();
    }

    public async Task ToggleActiveAsync(int id)
    {
        var project = await _context.Projects.FirstOrDefaultAsync(x => x.ProjectId == id);
        if (project == null) return;
        project.IsActive = !project.IsActive;
        await _context.SaveChangesAsync();
    }
}
