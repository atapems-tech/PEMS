using PEMS.Domain.Entities;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق گزارش سخت‌افزاری نهایی Package Procurement (Gap ۳): لایه‌ی مدیریتی روی
/// ContractApprovalAuthorityRule (که در Phase 4 فقط معماری/گیت آن ساخته شد، بدون هیچ راه
/// مدیریتی). هیچ آستانه‌ی پولی خاص شرکتی این‌جا اختراع/Seed نمی‌شود - فقط CRUD روی چیزی که یک
/// مدیر مجاز صریحاً تعریف می‌کند.
/// </summary>
public interface IContractApprovalAuthorityRuleService
{
    Task<List<ContractApprovalAuthorityRule>> GetAllAsync();

    Task<(bool Success, string? Error, int? RuleId)> CreateAsync(
        decimal? minValue, decimal? maxValue, string? criticalityClass, string requiredApproverRole,
        string? description, int priority);

    Task<(bool Success, string? Error)> UpdateAsync(
        int ruleId, decimal? minValue, decimal? maxValue, string? criticalityClass, string requiredApproverRole,
        string? description, int priority);

    Task<(bool Success, string? Error)> SetActiveAsync(int ruleId, bool isActive);
}
