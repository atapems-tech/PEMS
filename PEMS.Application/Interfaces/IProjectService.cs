using PEMS.Application.DTOs;
using PEMS.Domain.Entities;

namespace PEMS.Application.Interfaces;

public interface IProjectService
{
    Task<List<ProjectDto>> GetAllAsync();
    Task<Project?> GetEntityByIdAsync(int id);
    Task AddAsync(Project project);
    Task UpdateAsync(Project project);
    Task ToggleActiveAsync(int id);
}
