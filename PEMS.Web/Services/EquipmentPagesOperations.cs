using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-692: the equipment list and the passport over the same services the controller called (see <see cref="IEquipmentPagesOperations"/>).</summary>
public sealed class EquipmentPagesOperations(PEMSDbContext context, IEquipmentService equipment, IInspectionService inspections, IEquipmentClassificationService classification, IMaintenanceIntelligenceService intelligence) : IEquipmentPagesOperations
{
    private const int RowLimit = 5000;

    public async Task<List<EquipmentDto>> ListAsync() => (await equipment.GetPagedAsync(1, RowLimit)).Items.ToList();
    public Task<EquipmentPassportDto?> PassportAsync(int id) => equipment.GetPassportAsync(id);
    public Task<List<InspectionListItemDto>> InspectionsAsync(int id) => inspections.GetForEquipmentAsync(id);
    public Task<ReviewQueueItemDto?> ClassificationSuggestionAsync(int id) => classification.GetPendingSuggestionForEquipmentAsync(id);
    public Task<ClassificationExplanationDto?> ClassificationExplanationAsync(int id) => classification.ExplainClassificationAsync(id);
    public Task<EquipmentMaintenanceIntelligenceDto?> MaintenanceIntelligenceAsync(int id) => intelligence.GetForEquipmentAsync(id);

    public async Task<List<EquipmentChoice>> ProjectOptionsAsync()
        => await context.Projects.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.ProjectName).Select(x => new EquipmentChoice(x.ProjectId.ToString(), x.ProjectName)).ToListAsync();

    public async Task<List<EquipmentChoice>> DisciplineOptionsAsync()
        => await context.Disciplines.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new EquipmentChoice(x.DisciplineId.ToString(), x.Name)).ToListAsync();
}
