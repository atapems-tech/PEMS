using Microsoft.Extensions.Configuration;
using PEMS.AI.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۷ (AI Governance) - مهم‌ترین ضمانت این فایل: تایید/رد یک بینش AI
/// هرگز نباید هیچ رکورد معتبر دیگری (RCMDecision/MaintenanceStrategy/EquipmentCriticality/
/// FailureRecord/WorkOrder) را تغییر دهد. این تست‌ها آن ضمانت را با شمارش واقعی ردیف‌های آن
/// جداول قبل/بعد از ReviewInsightAsync اثبات می‌کنند - نه فقط با خواندن کد.
/// </summary>
public class AIReliabilityInsightServiceTests
{
    private class FakeOllamaService : IOllamaService
    {
        public string? NextResponse { get; set; } = "Investigate vibration trend on P-101\nReview PM interval for pump class";
        public bool ThrowOnGenerate { get; set; }

        public Task<string> GenerateAsync(string prompt)
        {
            if (ThrowOnGenerate) throw new InvalidOperationException("Ollama not reachable");
            return Task.FromResult(NextResponse ?? string.Empty);
        }

        public Task<string> GenerateAsync(string prompt, PEMS.AI.Models.OllamaGenerateOptions options) => GenerateAsync(prompt);

        public Task<float[]> GenerateEmbeddingAsync(string text) => Task.FromResult(Array.Empty<float>());
    }

    private static async Task<(AIReliabilityInsightService Service, PEMS.Persistence.Context.PEMSDbContext Ctx, FakeOllamaService Ollama)> CreateServiceAsync(string dbName, FakeOllamaService? ollama = null)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"AIInsight_{dbName}");
        ctx.Departments.Add(new Department { DepartmentId = 1, Name = "Maintenance" });
        ctx.Employees.Add(new Employee { EmployeeId = 1, FirstName = "A", LastName = "B", DepartmentId = 1, IsActive = true });
        ctx.Equipments.Add(new Equipment { EquipmentId = 1, TagNumber = "P-101", Name = "Pump", EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1, IsActive = true });
        await ctx.SaveChangesAsync();

        var reliabilityService = new ReliabilityService(ctx);
        var analyticsService = new ReliabilityAnalyticsService(ctx, reliabilityService);
        var intelligenceService = new ReliabilityIntelligenceService(ctx);
        var fakeOllama = ollama ?? new FakeOllamaService();
        var configuration = new ConfigurationBuilder().Build();

        return (new AIReliabilityInsightService(ctx, fakeOllama, analyticsService, intelligenceService, configuration), ctx, fakeOllama);
    }

    private static void AddFailure(PEMS.Persistence.Context.PEMSDbContext ctx, int id, DateTime date, decimal downtime = 2) =>
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = id, EquipmentId = 1, FailureDate = date, FailureMode = "Wear", DowntimeHours = downtime, IsActive = true, CreatedDate = DateTime.Now });

    [Fact]
    public async Task GenerateInsightsAsync_NoUnderlyingData_DoesNotCallOllama_ReturnsFailure()
    {
        var (service, _, ollama) = await CreateServiceAsync(nameof(GenerateInsightsAsync_NoUnderlyingData_DoesNotCallOllama_ReturnsFailure));
        ollama.NextResponse = "SHOULD NOT BE CALLED";

        var result = await service.GenerateInsightsAsync(null, "tester");

        Assert.False(result.Success);
        Assert.Equal(0, result.InsightsGenerated);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task GenerateInsightsAsync_WithRealFailureData_CallsOllama_PersistsDraftInsights()
    {
        var (service, ctx, _) = await CreateServiceAsync(nameof(GenerateInsightsAsync_WithRealFailureData_CallsOllama_PersistsDraftInsights));
        AddFailure(ctx, 1, new DateTime(2026, 1, 1));
        AddFailure(ctx, 2, new DateTime(2026, 2, 1));
        await ctx.SaveChangesAsync();

        var result = await service.GenerateInsightsAsync(null, "tester");

        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.InsightsGenerated);

        var insights = ctx.AIReliabilityInsights.ToList();
        Assert.Equal(2, insights.Count);
        Assert.All(insights, i => Assert.Equal("Draft", i.ReviewStatus));
        Assert.All(insights, i => Assert.False(string.IsNullOrEmpty(i.ModelName)));
        Assert.All(insights, i => Assert.False(string.IsNullOrEmpty(i.SourceDataSummary)));
    }

    [Fact]
    public async Task GenerateInsightsAsync_OllamaUnavailable_ReturnsErrorWithoutPersisting()
    {
        var ollama = new FakeOllamaService { ThrowOnGenerate = true };
        var (service, ctx, _) = await CreateServiceAsync(nameof(GenerateInsightsAsync_OllamaUnavailable_ReturnsErrorWithoutPersisting), ollama);
        AddFailure(ctx, 1, new DateTime(2026, 1, 1));
        AddFailure(ctx, 2, new DateTime(2026, 2, 1));
        await ctx.SaveChangesAsync();

        var result = await service.GenerateInsightsAsync(null, "tester");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Empty(ctx.AIReliabilityInsights);
    }

    [Fact]
    public async Task ReviewInsightAsync_Approve_OnlyModifiesInsightRow_NoOtherTableChanges()
    {
        var (service, ctx, _) = await CreateServiceAsync(nameof(ReviewInsightAsync_Approve_OnlyModifiesInsightRow_NoOtherTableChanges));
        var insight = new AIReliabilityInsight
        {
            InsightType = "InvestigationSuggestion", SuggestionText = "test", SourceDataSummary = "test",
            ModelName = "test-model", ReviewStatus = "Draft", GeneratedDate = DateTime.Now, IsActive = true, CreatedDate = DateTime.Now
        };
        ctx.AIReliabilityInsights.Add(insight);
        await ctx.SaveChangesAsync();

        var strategyCountBefore = ctx.MaintenanceStrategies.Count();
        var criticalityCountBefore = ctx.EquipmentCriticalities.Count();
        var rcmDecisionCountBefore = ctx.RCMDecisions.Count();
        var failureRecordCountBefore = ctx.FailureRecords.Count();
        var workOrderCountBefore = ctx.WorkOrders.Count();

        var (success, error) = await service.ReviewInsightAsync(insight.AIReliabilityInsightId, approve: true, reviewedByEmployeeId: 1, notes: "looks good");

        Assert.True(success, error);

        var reloaded = ctx.AIReliabilityInsights.Single(x => x.AIReliabilityInsightId == insight.AIReliabilityInsightId);
        Assert.Equal("Approved", reloaded.ReviewStatus);
        Assert.Equal(1, reloaded.ReviewedById);
        Assert.NotNull(reloaded.ReviewedDate);
        Assert.Equal("looks good", reloaded.ReviewComment);

        // طبق بند ۱۷: هیچ رکورد معتبر دیگری نباید تغییر کرده باشد
        Assert.Equal(strategyCountBefore, ctx.MaintenanceStrategies.Count());
        Assert.Equal(criticalityCountBefore, ctx.EquipmentCriticalities.Count());
        Assert.Equal(rcmDecisionCountBefore, ctx.RCMDecisions.Count());
        Assert.Equal(failureRecordCountBefore, ctx.FailureRecords.Count());
        Assert.Equal(workOrderCountBefore, ctx.WorkOrders.Count());
    }

    [Fact]
    public async Task ReviewInsightAsync_AlreadyReviewed_CannotBeReReviewed()
    {
        var (service, ctx, _) = await CreateServiceAsync(nameof(ReviewInsightAsync_AlreadyReviewed_CannotBeReReviewed));
        var insight = new AIReliabilityInsight
        {
            InsightType = "InvestigationSuggestion", SuggestionText = "test", SourceDataSummary = "test",
            ModelName = "test-model", ReviewStatus = "Approved", GeneratedDate = DateTime.Now, IsActive = true, CreatedDate = DateTime.Now
        };
        ctx.AIReliabilityInsights.Add(insight);
        await ctx.SaveChangesAsync();

        var (success, error) = await service.ReviewInsightAsync(insight.AIReliabilityInsightId, approve: false, reviewedByEmployeeId: 1, notes: null);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task GetInsightsAsync_FiltersByReviewStatus()
    {
        var (service, ctx, _) = await CreateServiceAsync(nameof(GetInsightsAsync_FiltersByReviewStatus));
        ctx.AIReliabilityInsights.Add(new AIReliabilityInsight { InsightType = "X", SuggestionText = "a", SourceDataSummary = "s", ModelName = "m", ReviewStatus = "Draft", GeneratedDate = DateTime.Now, IsActive = true, CreatedDate = DateTime.Now });
        ctx.AIReliabilityInsights.Add(new AIReliabilityInsight { InsightType = "X", SuggestionText = "b", SourceDataSummary = "s", ModelName = "m", ReviewStatus = "Approved", GeneratedDate = DateTime.Now, IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var draftOnly = await service.GetInsightsAsync("Draft");

        Assert.Single(draftOnly);
        Assert.Equal("Draft", draftOnly[0].ReviewStatus);
    }
}
