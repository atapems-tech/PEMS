using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-692, 693, 745: see <see cref="IReliabilityRegisterOperations"/>; the logic is the one of the controllers, moved without change.</summary>
public sealed class ReliabilityRegisterOperations(PEMSDbContext context, IEquipmentService equipment, IRelationshipService relationships, ICurrentUserService currentUser) : IReliabilityRegisterOperations
{
    public async Task<(bool Success, string? Error)> AssessCriticalityAsync(int equipmentId, string criticalityClass, string? riskLevel = null, string? safetyImpact = null, string? productionImpact = null,
        string? environmentImpact = null, string? description = null)
    {
        if (!await context.Equipments.AnyAsync(x => x.EquipmentId == equipmentId && x.IsActive))
            return (false, "تجهیز انتخاب‌شده نامعتبر یا غیرفعال است.");

        // one active analysis per equipment: the previous one is replaced, not repeated
        var existing = await context.EquipmentCriticalities.FirstOrDefaultAsync(x => x.EquipmentId == equipmentId && x.IsActive);
        if (existing != null) existing.IsActive = false;

        await context.EquipmentCriticalities.AddAsync(new EquipmentCriticality
        {
            EquipmentId = equipmentId, CriticalityClass = criticalityClass, RiskLevel = riskLevel, SafetyImpact = safetyImpact, ProductionImpact = productionImpact,
            EnvironmentImpact = environmentImpact, Description = description, IsActive = true, CreatedDate = DateTime.Now
        });

        // the filtered unique index (one active record per equipment) refuses a concurrent second analysis
        try { await context.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            return (false, "هم‌زمان یک تحلیل بحرانی‌بودن فعال دیگر برای این تجهیز ثبت شد. لطفاً دوباره تلاش کنید.");
        }
        return (true, null);
    }

    public Task<(bool Success, string? Error)> AssignFoundationAsync(int equipmentId, int? functionalLocationId = null, int? parentEquipmentId = null, DateTime? installationDate = null)
        => equipment.AssignFoundationAsync(equipmentId, functionalLocationId, parentEquipmentId, installationDate, currentUser.UserName);

    public async Task<(bool Success, string? Error)> ConfirmSuggestedLinkAsync(int equipmentId, string targetType, int targetId, bool force = false)
    {
        try
        {
            await relationships.ConfirmSuggestedLinkAsync(equipmentId, targetType, targetId, force);
            return (true, null);
        }
        catch (Exception ex)
        {
            // the message carries the hint to confirm again with force (audit item 1.4)
            return (false, ex.Message);
        }
    }
}
