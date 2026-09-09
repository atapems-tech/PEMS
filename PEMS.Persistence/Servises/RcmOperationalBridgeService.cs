using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 6» بند ۱۳/۵۳-۵۴: پیاده‌سازی پل RCM→عملیاتی.
///
/// قاعده‌ی بازاستفاده (بند ۵۴): کلید تشخیص «آیا استراتژی معادل از قبل موجود است» دقیقاً همان
/// چیزی است که خودِ MaintenanceStrategy از ابتدا طراحی شده بود تا نشان دهد - Equipment +
/// FailureModeCode (طبق کامنت خودِ Entity: «هر استراتژی به یک حالت خرابی مشخص وصل می‌شه»؛
/// نه یک کلید مصنوعی جدید). اگر موجود بود، بازاستفاده/به‌روزرسانی می‌شود؛ هرگز رکورد تکراری
/// ساخته نمی‌شود.
///
/// اگر RCMDecision یک PMTemplate واقعی توصیه کرده باشد (RecommendedPMTemplateId)، طبق طراحی
/// فاز ۲ (PMTemplate.TriggerType/IntervalDays/MeterType/MeterInterval دقیقاً همنام
/// MaintenancePlan) یک MaintenancePlan هم با کپی مستقیم همان مقادیر واقعی ساخته/به‌روز می‌شود -
/// هرگز یک تناوب جعلی اختراع نمی‌شود.
/// </summary>
public class RcmOperationalBridgeService : IRcmOperationalBridgeService
{
    private readonly PEMSDbContext _context;
    private readonly INumberingService _numberingService;

    public RcmOperationalBridgeService(PEMSDbContext context, INumberingService numberingService)
    {
        _context = context;
        _numberingService = numberingService;
    }

    public async Task<ApplyRcmDecisionResultDto> ApplyApprovedDecisionAsync(int rcmDecisionId, int equipmentId, string appliedBy)
    {
        var decision = await _context.RCMDecisions
            .Include(x => x.RCMAnalysis)
            .Include(x => x.MaintenanceStrategyType)
            .Include(x => x.RecommendedPMTemplate)
            .Include(x => x.FMEAItem!).ThenInclude(x => x.FailureMode)
            .FirstOrDefaultAsync(x => x.RCMDecisionId == rcmDecisionId);

        if (decision == null)
            return new ApplyRcmDecisionResultDto { Success = false, Error = "RCM Decision یافت نشد." };

        // طبق بند ۲۰/۵۳: فقط توصیه‌ی تاییدشده قابل اعمال است - هرگز یک Draft.
        if (decision.Status != "APPROVED")
            return new ApplyRcmDecisionResultDto { Success = false, Error = $"فقط تصمیمات RCM با وضعیت APPROVED قابل اعمال هستند (وضعیت فعلی: {decision.Status})." };

        var equipment = await _context.Equipments.FirstOrDefaultAsync(x => x.EquipmentId == equipmentId && x.IsActive);
        if (equipment == null)
            return new ApplyRcmDecisionResultDto { Success = false, Error = "تجهیز یافت نشد یا فعال نیست." };

        // طبق بند ۵۳: این توصیه فقط برای همان EquipmentClass (یا کلاسی که تحلیل RCM برایش انجام
        // شده) معتبر است - هرگز به تجهیزی از کلاس نامرتبط اعمال نمی‌شود.
        if (equipment.EquipmentClassId != decision.RCMAnalysis?.EquipmentClassId)
            return new ApplyRcmDecisionResultDto { Success = false, Error = "کلاس تجهیز با کلاس تحلیل RCM این تصمیم مطابقت ندارد." };

        var failureModeCode = decision.FMEAItem?.FailureMode?.Code;
        var result = new ApplyRcmDecisionResultDto();

        // ==================== MaintenanceStrategy: بازاستفاده یا ساخت ====================
        var existingStrategy = await _context.MaintenanceStrategies
            .FirstOrDefaultAsync(x => x.EquipmentId == equipmentId && x.IsActive &&
                x.FailureModeCode == failureModeCode);

        // طبق بند ۵۱: appliedBy جدا از CreatedBy/ModifiedBy خودکار BaseEntity نگه داشته می‌شود
        // چون معنای متفاوتی دارد - CreatedBy یعنی «چه کسی این رکورد EF را ساخت»، اینجا یعنی
        // «چه کسی تصمیم گرفت این توصیه‌ی RCM را اعمال کند» (ممکن است شخص دیگری باشد).
        var justificationNote = $"Applied by {appliedBy} from RCM Decision #{decision.RCMDecisionId} (approved by {decision.ApprovedBy} on {decision.ApprovedAt:yyyy-MM-dd}). {decision.Rationale}".Trim();

        if (existingStrategy != null)
        {
            existingStrategy.MaintenanceStrategyTypeId = decision.MaintenanceStrategyTypeId;
            existingStrategy.ConsequenceCategory = decision.ConsequenceCategory;
            existingStrategy.Justification = justificationNote;
            existingStrategy.StrategyType = decision.MaintenanceStrategyType?.Code ?? decision.DecisionType;

            result.MaintenanceStrategyId = existingStrategy.MaintenanceStrategyId;
            result.MaintenanceStrategyWasReused = true;
        }
        else
        {
            var newStrategy = new MaintenanceStrategy
            {
                EquipmentId = equipmentId,
                StrategyType = decision.MaintenanceStrategyType?.Code ?? decision.DecisionType,
                MaintenanceStrategyTypeId = decision.MaintenanceStrategyTypeId,
                FailureModeCode = failureModeCode,
                ConsequenceCategory = decision.ConsequenceCategory,
                Justification = justificationNote,
                RecommendedTriggerType = "Calendar",
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            _context.MaintenanceStrategies.Add(newStrategy);
            await _context.SaveChangesAsync();

            result.MaintenanceStrategyId = newStrategy.MaintenanceStrategyId;
            result.MaintenanceStrategyWasReused = false;
        }

        await _context.SaveChangesAsync();

        // ==================== MaintenancePlan: فقط اگر یک PMTemplate واقعی توصیه شده باشد ====================
        if (decision.RecommendedPMTemplateId.HasValue && decision.RecommendedPMTemplate != null)
        {
            var template = decision.RecommendedPMTemplate;

            var existingPlan = await _context.MaintenancePlans
                .FirstOrDefaultAsync(x => x.EquipmentId == equipmentId && x.IsActive &&
                    x.MaintenanceStrategyId == result.MaintenanceStrategyId);

            if (existingPlan != null)
            {
                existingPlan.JobPlanId = template.JobPlanId;
                existingPlan.TriggerType = template.TriggerType;
                existingPlan.IntervalDays = template.IntervalDays;
                existingPlan.MeterType = template.MeterType;
                existingPlan.MeterInterval = template.MeterInterval;

                result.MaintenancePlanId = existingPlan.MaintenancePlanId;
                result.MaintenancePlanWasReused = true;
            }
            else
            {
                var planNumber = await _numberingService.GetNextNumberAsync("MaintenancePlan", equipment.TagNumber);

                var newPlan = new MaintenancePlan
                {
                    EquipmentId = equipmentId,
                    MaintenanceStrategyId = result.MaintenanceStrategyId!.Value,
                    PlanNumber = planNumber,
                    Title = $"{template.Name} (from RCM Decision #{decision.RCMDecisionId})",
                    JobPlanId = template.JobPlanId,
                    TriggerType = template.TriggerType,
                    IntervalDays = template.IntervalDays,
                    MeterType = template.MeterType,
                    MeterInterval = template.MeterInterval,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                _context.MaintenancePlans.Add(newPlan);
                await _context.SaveChangesAsync();

                result.MaintenancePlanId = newPlan.MaintenancePlanId;
                result.MaintenancePlanWasReused = false;
            }

            await _context.SaveChangesAsync();
        }

        result.Success = true;
        return result;
    }
}
