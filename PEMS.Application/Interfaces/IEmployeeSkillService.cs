namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق PEMS_CMMS_MAINTENANCE_ORGANIZATION_BLUEPRINT.md بخش ۱۷: همون منطق سه‌حالته‌ی
/// ITeamService (Reject/Reactivate/New) برای تخصیص مهارت به کارمند.
/// </summary>
public interface IEmployeeSkillService
{
    Task AddOrReactivateAsync(int employeeId, int skillId, string proficiencyLevel, DateTime? certifiedDate);

    Task RemoveAsync(int employeeSkillId);
}
