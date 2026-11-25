using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-744: the coverage matrix and the class detail of RcmFmeaController as read operations over the same services and tables (see <see cref="IRcmFmeaPagesOperations"/>).</summary>
public sealed class RcmFmeaPagesOperations(PEMSDbContext context, IRcmFmeaReadinessService readiness, IFMEAService fmea, IRCMService rcm) : IRcmFmeaPagesOperations
{
    public async Task<RcmFmeaCoverageMatrixDto> CoverageAsync()
    {
        var matrix = await readiness.GetCoverageMatrixAsync();
        matrix.Classes = matrix.Classes.Where(c => c.EquipmentCount > 0).ToList();   // the page listed only the classes that have equipment
        return matrix;
    }

    public Task<EquipmentClassRcmFmeaReadinessDto?> ReadinessAsync(int equipmentClassId) => readiness.GetForEquipmentClassAsync(equipmentClassId);

    public async Task<List<RcmFunctionalFailureRow>> FunctionalFailuresAsync(int equipmentClassId)
        => await context.FunctionalFailures.AsNoTracking().Where(x => x.IsActive && x.FunctionalRequirement!.EquipmentClassId == equipmentClassId).OrderBy(x => x.Title)
            .Select(x => new RcmFunctionalFailureRow(x.FunctionalFailureId, x.FunctionalRequirement!.Code, x.Code, x.Title, x.FailureDefinition)).ToListAsync();

    public Task<List<FMEAAnalysisDto>> FmeaAnalysesAsync(int equipmentClassId) => fmea.GetForEquipmentClassAsync(equipmentClassId);

    public async Task<List<RcmFmeaItemRow>> FmeaItemsAsync(int equipmentClassId)
        => (await fmea.GetForEquipmentClassAsync(equipmentClassId)).SelectMany(a => a.Items.Select(i => new RcmFmeaItemRow(i.FMEAItemId, a.Title, a.Revision, a.Status, i.FailureModeName, i.FailureCauseName, i.FailureEffectName,
            i.FailureConsequenceName, i.Severity, i.Occurrence, i.Detectability, i.RiskPriorityNumber, i.MaintenanceStrategyTypeName))).ToList();

    public Task<List<RCMAnalysisDto>> RcmAnalysesAsync(int equipmentClassId) => rcm.GetForEquipmentClassAsync(equipmentClassId);

    public async Task<List<RcmDecisionRow>> RcmDecisionsAsync(int equipmentClassId)
        => (await rcm.GetForEquipmentClassAsync(equipmentClassId)).SelectMany(r => r.Decisions.Select(d => new RcmDecisionRow(d.RCMDecisionId, r.Revision, r.Status, d.FailureModeName, d.DecisionType, d.MaintenanceStrategyTypeName,
            d.RecommendedPMTemplateName, d.ConsequenceCategory, d.Status))).ToList();

    public async Task<List<RcmChoice>> FailureModeOptionsAsync(int equipmentClassId)
        => await context.FailureModes.AsNoTracking().Where(x => x.IsActive && x.EquipmentClassId == equipmentClassId).OrderBy(x => x.Name).Select(x => new RcmChoice(x.FailureModeId.ToString(), x.Name)).ToListAsync();

    public async Task<List<RcmChoice>> FunctionalFailureOptionsAsync(int equipmentClassId)
        => await context.FunctionalFailures.AsNoTracking().Where(x => x.IsActive && x.FunctionalRequirement!.EquipmentClassId == equipmentClassId).OrderBy(x => x.Title).Select(x => new RcmChoice(x.FunctionalFailureId.ToString(), x.Title)).ToListAsync();

    public async Task<List<RcmChoice>> FunctionalRequirementOptionsAsync(int equipmentClassId)
        => await context.FunctionalRequirements.AsNoTracking().Where(x => x.IsActive && x.EquipmentClassId == equipmentClassId).OrderBy(x => x.Code).Select(x => new RcmChoice(x.FunctionalRequirementId.ToString(), x.Code + " - " + x.Title)).ToListAsync();

    public async Task<List<RcmChoice>> StrategyTypeOptionsAsync()
        => await context.MaintenanceStrategyTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new RcmChoice(x.MaintenanceStrategyTypeId.ToString(), x.Name)).ToListAsync();

    public async Task<List<RcmChoice>> PmTemplateOptionsAsync(int equipmentClassId)
        => await context.PMTemplates.AsNoTracking().Where(x => x.IsActive && x.EquipmentClassId == equipmentClassId).OrderBy(x => x.Name).Select(x => new RcmChoice(x.PMTemplateId.ToString(), x.Name)).ToListAsync();

    public async Task<List<RcmChoice>> EquipmentOptionsAsync(int equipmentClassId)
        => await context.Equipments.AsNoTracking().Where(x => x.IsActive && x.EquipmentClassId == equipmentClassId).OrderBy(x => x.TagNumber).Select(x => new RcmChoice(x.EquipmentId.ToString(), x.TagNumber + " - " + x.Name)).ToListAsync();

    public async Task<List<RcmChoice>> DraftFmeaOptionsAsync(int equipmentClassId)
        => (await fmea.GetForEquipmentClassAsync(equipmentClassId)).Where(a => a.Status == "DRAFT").Select(a => new RcmChoice(a.FMEAAnalysisId.ToString(), $"{a.Title} (Rev. {a.Revision})")).ToList();

    public async Task<List<RcmChoice>> ApprovedFmeaOptionsAsync(int equipmentClassId)
        => (await fmea.GetForEquipmentClassAsync(equipmentClassId)).Where(a => a.Status == "APPROVED").Select(a => new RcmChoice(a.FMEAAnalysisId.ToString(), $"{a.Title} (Rev. {a.Revision})")).ToList();

    public async Task<List<RcmChoice>> ApprovedFmeaItemOptionsAsync(int equipmentClassId)
        => (await fmea.GetForEquipmentClassAsync(equipmentClassId)).Where(a => a.Status == "APPROVED").SelectMany(a => a.Items.Select(i => new RcmChoice(i.FMEAItemId.ToString(), $"{i.FailureModeName} ({a.Title})"))).ToList();

    public async Task<List<RcmChoice>> DraftRcmOptionsAsync(int equipmentClassId)
        => (await rcm.GetForEquipmentClassAsync(equipmentClassId)).Where(r => r.Status == "DRAFT").Select(r => new RcmChoice(r.RCMAnalysisId.ToString(), $"RCM Rev. {r.Revision}")).ToList();
}
