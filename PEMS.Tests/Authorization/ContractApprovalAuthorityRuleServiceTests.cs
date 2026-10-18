using PEMS.Persistence.Identity;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق گزارش سخت‌افزاری نهایی Package Procurement (Gap ۳): CRUD/اعتبارسنجی/انتخاب قاعده روی
/// ContractApprovalAuthorityRule. هیچ آستانه‌ی پولی خاص شرکتی این‌جا تست/اختراع نمی‌شود - فقط
/// رفتار معماری با مقادیر دلخواه.
/// </summary>
public class ContractApprovalAuthorityRuleServiceTests
{
    private static (PEMS.Persistence.Context.PEMSDbContext Ctx, ContractApprovalAuthorityRuleService Service) Create(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"ApprovalAuthority_{dbName}");
        return (ctx, new ContractApprovalAuthorityRuleService(ctx));
    }

    [Fact]
    public async Task Create_WithUnknownRole_Fails()
    {
        var (_, service) = Create(nameof(Create_WithUnknownRole_Fails));

        var result = await service.CreateAsync(null, 100000m, null, "NotARealRole", null, 0);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Create_WithMinGreaterThanMax_Fails()
    {
        var (_, service) = Create(nameof(Create_WithMinGreaterThanMax_Fails));

        var result = await service.CreateAsync(500000m, 100000m, null, SystemRoles.ContractManager, null, 0);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Create_ExactDuplicateRange_Fails()
    {
        var (_, service) = Create(nameof(Create_ExactDuplicateRange_Fails));
        await service.CreateAsync(0m, 100000m, "A", SystemRoles.ContractManager, "first", 0);

        var result = await service.CreateAsync(0m, 100000m, "A", SystemRoles.EngineeringManager, "duplicate", 1);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Create_ValidRule_Succeeds()
    {
        var (ctx, service) = Create(nameof(Create_ValidRule_Succeeds));

        var result = await service.CreateAsync(0m, 100000m, "A", SystemRoles.ContractManager, "Low-value critical", 0);

        Assert.True(result.Success);
        Assert.Single(ctx.ContractApprovalAuthorityRules);
    }

    [Fact]
    public async Task SetActive_DeactivatesRule()
    {
        var (ctx, service) = Create(nameof(SetActive_DeactivatesRule));
        var created = await service.CreateAsync(null, null, null, SystemRoles.ContractManager, null, 0);

        var result = await service.SetActiveAsync(created.RuleId!.Value, false);

        Assert.True(result.Success);
        Assert.False(ctx.ContractApprovalAuthorityRules.Single().IsActive);
    }

    [Fact]
    public async Task Update_ChangesFields()
    {
        var (ctx, service) = Create(nameof(Update_ChangesFields));
        var created = await service.CreateAsync(0m, 100000m, "A", SystemRoles.ContractManager, "original", 0);

        var result = await service.UpdateAsync(created.RuleId!.Value, 0m, 200000m, "A", SystemRoles.EngineeringManager, "updated", 5);

        Assert.True(result.Success);
        var rule = ctx.ContractApprovalAuthorityRules.Single();
        Assert.Equal(200000m, rule.MaxValue);
        Assert.Equal(SystemRoles.EngineeringManager, rule.RequiredApproverRole);
        Assert.Equal(5, rule.Priority);
    }
}
