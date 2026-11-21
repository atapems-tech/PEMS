using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-744: the write commands of RcmFmeaController, the logic moved without change (see <see cref="IRcmFmeaOperations"/>).</summary>
public sealed class RcmFmeaOperations(PEMSDbContext context, IFMEAService fmea, IRCMService rcm, IRcmOperationalBridgeService bridge, ICurrentUserService currentUser) : IRcmFmeaOperations
{
    private string User => currentUser.UserName ?? "Unknown";

    public async Task<(bool Success, string? Error)> CreateFunctionalRequirementAsync(int equipmentClassId, string code, string title, string? description = null, string? requiredFunction = null)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title)) return (false, "کد و عنوان الزامی هستند.");
        if (await context.FunctionalRequirements.AnyAsync(x => x.EquipmentClassId == equipmentClassId && x.Code == code.Trim()))
            return (false, "Functional Requirement ای با همین کد از قبل روی این کلاس وجود دارد.");
        context.FunctionalRequirements.Add(new Domain.Entities.FunctionalRequirement
        {
            EquipmentClassId = equipmentClassId, Code = code.Trim(), Title = title.Trim(), Description = description, RequiredFunction = requiredFunction, IsActive = true, CreatedDate = DateTime.Now
        });
        await context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CreateFunctionalFailureAsync(int functionalRequirementId, string code, string title, string? failureDefinition = null)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title)) return (false, "کد و عنوان الزامی هستند.");
        context.FunctionalFailures.Add(new Domain.Entities.FunctionalFailure
        {
            FunctionalRequirementId = functionalRequirementId, Code = code.Trim(), Title = title.Trim(), FailureDefinition = failureDefinition, IsActive = true, CreatedDate = DateTime.Now
        });
        await context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CreateFmeaDraftAsync(int equipmentClassId, string title, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(title)) return (false, "عنوان تحلیل FMEA الزامی است.");
        await fmea.CreateDraftAsync(equipmentClassId, title.Trim(), description, User);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AddFmeaItemAsync(int fmeaAnalysisId, int failureModeId, int? functionalFailureId = null, string? detectionMethod = null, string? existingControl = null,
        string? recommendedAction = null, int? severity = null, int? occurrence = null, int? detectability = null, int? maintenanceStrategyTypeId = null, int? standardReferenceId = null)
    {
        // cause, mechanism, effect and consequence come from the failure mode itself (the first linked cause and mechanism are the default)
        var failureMode = await context.FailureModes.AsNoTracking().Include(x => x.Causes).FirstOrDefaultAsync(x => x.FailureModeId == failureModeId);
        if (failureMode == null) return (false, "Failure Mode یافت نشد.");
        var firstCauseId = failureMode.Causes.Select(x => (int?)x.FailureCauseId).FirstOrDefault();
        int? firstMechanismId = null;
        if (firstCauseId.HasValue)
            firstMechanismId = await context.FailureCauseMechanisms.Where(x => x.FailureCauseId == firstCauseId.Value).Select(x => (int?)x.FailureMechanismId).FirstOrDefaultAsync();
        try
        {
            await fmea.AddItemAsync(fmeaAnalysisId, failureModeId, functionalFailureId, firstCauseId, firstMechanismId, failureMode.FailureEffectId, failureMode.FailureConsequenceId,
                detectionMethod, existingControl, recommendedAction, severity, occurrence, detectability, maintenanceStrategyTypeId, standardReferenceId);
            return (true, null);
        }
        catch (InvalidOperationException ex) { return (false, ex.Message); }
    }

    public async Task<(bool Success, string? Error)> SubmitFmeaAsync(int fmeaAnalysisId)
        => await fmea.SubmitForReviewAsync(fmeaAnalysisId, User) ? (true, null) : (false, "این تحلیل در وضعیت DRAFT نیست.");

    public async Task<(bool Success, string? Error)> ApproveFmeaAsync(int fmeaAnalysisId)
        => await fmea.ApproveAsync(fmeaAnalysisId, User) ? (true, null) : (false, "این تحلیل آماده‌ی تایید نیست.");

    public async Task<(bool Success, string? Error)> RejectFmeaAsync(int fmeaAnalysisId)
        => await fmea.RejectAsync(fmeaAnalysisId, User) ? (true, null) : (false, "این تحلیل آماده‌ی رد نیست.");

    public async Task<(bool Success, string? Error)> CreateRcmDraftAsync(int equipmentClassId, int fmeaAnalysisId)
    {
        await rcm.CreateDraftAsync(equipmentClassId, fmeaAnalysisId, User);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AddRcmDecisionAsync(int rcmAnalysisId, int fmeaItemId, string decisionType, string consequenceCategory, int? maintenanceStrategyTypeId = null,
        int? recommendedPMTemplateId = null, string? rationale = null)
    {
        try
        {
            await rcm.AddDecisionAsync(rcmAnalysisId, fmeaItemId, decisionType, maintenanceStrategyTypeId, recommendedPMTemplateId, rationale, consequenceCategory);
            return (true, null);
        }
        catch (InvalidOperationException ex) { return (false, ex.Message); }
    }

    public async Task<(bool Success, string? Error)> ApproveRcmDecisionAsync(int rcmDecisionId)
        => await rcm.ApproveDecisionAsync(rcmDecisionId, User) ? (true, null) : (false, "این تصمیم آماده‌ی تایید نیست.");

    public async Task<(bool Success, string? Error)> SubmitRcmAsync(int rcmAnalysisId)
        => await rcm.SubmitForReviewAsync(rcmAnalysisId, User) ? (true, null) : (false, "این تحلیل در وضعیت DRAFT نیست.");

    public async Task<(bool Success, string? Error)> ApproveRcmAsync(int rcmAnalysisId)
        => await rcm.ApproveAsync(rcmAnalysisId, User) ? (true, null) : (false, "این تحلیل آماده‌ی تایید نیست.");

    public async Task<(bool Success, string? Error)> RejectRcmAsync(int rcmAnalysisId)
        => await rcm.RejectAsync(rcmAnalysisId, User) ? (true, null) : (false, "این تحلیل آماده‌ی رد نیست.");

    // the only way an RCM decision becomes operational; the bridge service re-checks the APPROVED status and the equipment class itself
    public async Task<(bool Success, string? Error)> ApplyRcmDecisionAsync(int rcmDecisionId, int equipmentId)
    {
        var result = await bridge.ApplyApprovedDecisionAsync(rcmDecisionId, equipmentId, User);
        return result.Success ? (true, null) : (false, result.Error);
    }
}
