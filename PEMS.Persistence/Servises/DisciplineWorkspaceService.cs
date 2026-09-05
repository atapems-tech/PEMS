using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class DisciplineWorkspaceService : IDisciplineWorkspaceService
{
    private readonly PEMSDbContext _context;

    public DisciplineWorkspaceService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<DisciplineSummaryDto>> GetSummaryListAsync()
    {
        var disciplines = await _context.Disciplines
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        var result = new List<DisciplineSummaryDto>();

        foreach (var d in disciplines)
        {
            var docsQuery = _context.EngineeringDocuments.Where(x => x.DisciplineId == d.DisciplineId && x.IsActive);

            var tasksQuery = _context.DisciplineTasks.Where(x => x.DisciplineId == d.DisciplineId && x.IsActive);

            result.Add(new DisciplineSummaryDto
            {
                DisciplineId = d.DisciplineId,
                Code = d.Code,
                Name = d.Name,
                DocumentsCount = await docsQuery.CountAsync(),
                PendingDocumentsCount = await docsQuery.CountAsync(x => x.Status == "Submitted" || x.Status == "Checked"),
                EquipmentCount = await _context.Equipments.CountAsync(x => x.DisciplineId == d.DisciplineId && x.IsActive),
                VDRItemsCount = await _context.VDRItems.CountAsync(x =>
                    x.DisciplineId == d.DisciplineId || x.DisciplineCodeRaw == d.Code),
                OpenTasksCount = await tasksQuery.CountAsync(x => x.Status != "Completed"),
                OverdueTasksCount = await tasksQuery.CountAsync(x =>
                    x.Status != "Completed" && x.DueDate != null && x.DueDate < DateTime.Now)
            });
        }

        return result;
    }

    public async Task<DisciplineWorkspaceDto?> GetWorkspaceAsync(int disciplineId, int? projectId = null)
    {
        var discipline = await _context.Disciplines.FirstOrDefaultAsync(x => x.DisciplineId == disciplineId);
        if (discipline == null) return null;

        var summary = (await GetSummaryListAsync()).FirstOrDefault(x => x.DisciplineId == disciplineId)
            ?? new DisciplineSummaryDto { DisciplineId = disciplineId, Code = discipline.Code, Name = discipline.Name };

        var workspace = new DisciplineWorkspaceDto { Summary = summary };

        var docsQuery = _context.EngineeringDocuments
            .Include(x => x.Project)
            .Include(x => x.Discipline)
            .Where(x => x.DisciplineId == disciplineId && x.IsActive)
            .AsQueryable();

        if (projectId.HasValue) docsQuery = docsQuery.Where(x => x.ProjectId == projectId.Value);

        workspace.Documents = (await docsQuery.OrderByDescending(x => x.CreatedDate).Take(50).ToListAsync())
            .Select(d => new EngineeringDocumentDto
            {
                DocumentId = d.DocumentId,
                DocumentNo = d.DocumentNo,
                Title = d.Title,
                DocumentType = d.DocumentType,
                ProjectId = d.ProjectId,
                ProjectName = d.Project?.ProjectName ?? "",
                DisciplineId = d.DisciplineId ?? 0,
                DisciplineName = d.Discipline?.Name ?? "",
                Status = d.Status,
                RevisionNo = d.RevisionNo,
                CreatedDate = d.CreatedDate
            }).ToList();

        var equipmentQuery = _context.Equipments
            .Include(x => x.Project)
            .Include(x => x.Discipline)
            .Where(x => x.DisciplineId == disciplineId && x.IsActive)
            .AsQueryable();

        if (projectId.HasValue) equipmentQuery = equipmentQuery.Where(x => x.ProjectId == projectId.Value);

        workspace.Equipment = (await equipmentQuery.ToListAsync()).Select(e => new EquipmentDto
        {
            EquipmentId = e.EquipmentId,
            TagNumber = e.TagNumber,
            Name = e.Name,
            EquipmentType = e.EquipmentType,
            ProjectId = e.ProjectId,
            ProjectName = e.Project?.ProjectName ?? "",
            DisciplineId = e.DisciplineId,
            DisciplineName = e.Discipline?.Name ?? "",
            Status = e.Status
        }).ToList();

        var vdrQuery = _context.VDRItems
            .Include(x => x.Vendor)
            .Include(x => x.Submissions)
            .Include(x => x.Project)
            .Where(x => x.IsActive && (x.DisciplineId == disciplineId || x.DisciplineCodeRaw == discipline.Code))
            .AsQueryable();

        if (projectId.HasValue) vdrQuery = vdrQuery.Where(x => x.ProjectId == projectId.Value);

        workspace.VDRItems = (await vdrQuery.Take(50).ToListAsync()).Select(v =>
        {
            var latest = v.Submissions.OrderBy(s => s.RevisionNo).LastOrDefault();

            return new VDRItemDto
            {
                VDRItemId = v.DocumentId,
                DocumentNumber = v.DocumentNumber,
                DocumentDescription = v.DocumentDescription,
                VendorName = v.Vendor?.Name ?? "",
                WorkflowStatus = v.WorkflowStatus,
                LatestRevisionNo = latest?.RevisionNo,
                LatestReviewCategory = latest?.ReviewCategory,
                ProjectName = v.Project?.ProjectName ?? ""
            };
        }).ToList();

        var mrQuery = _context.MaterialRequisitions
            .Include(x => x.Project)
            .Include(x => x.Discipline)
            .Where(x => x.DisciplineId == disciplineId)
            .AsQueryable();

        if (projectId.HasValue) mrQuery = mrQuery.Where(x => x.ProjectId == projectId.Value);

        workspace.MRs = (await mrQuery.Take(50).ToListAsync()).Select(m => new MRDto
        {
            MRId = m.MRId,
            MRNumber = m.MRNumber,
            ProjectName = m.Project?.ProjectName ?? "",
            DisciplineName = m.Discipline?.Name ?? "",
            EquipmentTagNumber = m.EquipmentTagNumber,
            Description = m.Description,
            Status = m.Status,
            RequiredDate = m.RequiredDate
        }).ToList();

        var taskQuery = _context.DisciplineTasks
            .Include(x => x.Project)
            .Include(x => x.ResponsibleEmployee)
            .Where(x => x.DisciplineId == disciplineId && x.IsActive)
            .AsQueryable();

        if (projectId.HasValue) taskQuery = taskQuery.Where(x => x.ProjectId == projectId.Value);

        workspace.Tasks = (await taskQuery.OrderBy(x => x.DueDate).ToListAsync()).Select(t => new DisciplineTaskDto
        {
            DisciplineTaskId = t.DisciplineTaskId,
            DisciplineId = t.DisciplineId,
            ProjectId = t.ProjectId,
            ProjectName = t.Project?.ProjectName,
            Title = t.Title,
            Description = t.Description,
            Type = t.Type,
            ResponsibleName = t.ResponsibleEmployee != null
                ? t.ResponsibleEmployee.FirstName + " " + t.ResponsibleEmployee.LastName
                : null,
            DueDate = t.DueDate,
            Status = t.Status,
            IsOverdue = t.Status != "Completed" && t.DueDate.HasValue && t.DueDate.Value < DateTime.Now
        }).ToList();

        return workspace;
    }

    public async Task<int> AddTaskAsync(int disciplineId, int? projectId, string title, string? description,
        string type, int? responsibleEmployeeId, DateTime? dueDate)
    {
        var task = new DisciplineTask
        {
            DisciplineId = disciplineId,
            ProjectId = projectId,
            Title = title,
            Description = description,
            Type = string.IsNullOrWhiteSpace(type) ? "Task" : type,
            ResponsibleEmployeeId = responsibleEmployeeId,
            DueDate = dueDate,
            Status = "Open",
            CreatedDate = DateTime.Now
        };

        await _context.DisciplineTasks.AddAsync(task);
        await _context.SaveChangesAsync();

        return task.DisciplineTaskId;
    }

    public async Task SetTaskStatusAsync(int taskId, string status)
    {
        var task = await _context.DisciplineTasks.FirstOrDefaultAsync(x => x.DisciplineTaskId == taskId);
        if (task == null) return;

        task.Status = status;

        if (status == "Completed")
            task.CompletedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }
}
