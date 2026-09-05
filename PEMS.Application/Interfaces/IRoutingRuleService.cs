using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface IRoutingRuleService
{
    Task<List<RoutingRuleDto>> GetAllAsync();

    Task SetRuleAsync(string category, string targetModule, bool allowAutoRouting, string? description);

    Task<string?> GetTargetModuleAsync(string category);

    Task<bool> AllowsAutoRoutingAsync(string category);

    Task<AIConfidenceSettingsDto> GetConfidenceSettingsAsync();

    Task UpdateConfidenceSettingsAsync(double highThreshold, double mediumThreshold);

    Task SeedDefaultsAsync();
}
