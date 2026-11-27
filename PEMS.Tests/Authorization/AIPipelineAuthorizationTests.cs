using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;
using PEMS.Web.Security;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS — P0 AI Pipeline Endpoint Hardening»: AIExtractionController/TableExtractionController/
/// RelationshipController/EngineeringDocumentIntelligenceController/OCRProcessingController هیچ‌کدام
/// بررسی مجوزی نداشتند - Approve/ConfirmRow/BulkConfirm/ConfirmSuggestedLink/ApproveClassification/
/// AdvanceStage/Retry (که پیشنهاد AI را به داده‌ی معتبر تبدیل می‌کنند یا Job پس‌زمینه صف می‌کنند)
/// برای هر کاربر احرازهویت‌شده‌ای باز بودند. همون الگوی ImportPipelineAuthorizationTests.cs.
/// </summary>
public class AIPipelineAuthorizationTests
{
    private static async Task<(PEMSDbContext Ctx, IPermissionService Permissions, ICurrentUserService CurrentUser)>
        CreateInfra(string dbName, string? role)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"AIPipelineAuth_{dbName}");
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();

        var accessControl = new AccessControlService(ctx);
        var permissionService = new PermissionService(ctx, accessControl);
        await permissionService.SeedDefaultsAsync();

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());

        var currentUser = new FakeCurrentUserService { Roles = role != null ? new List<string> { role } : new List<string>() };
        return (ctx, permissionService, currentUser);
    }

    private class FakeDocumentAIIngestionService : IDocumentAIIngestionService
    {
        public bool ApproveTagCalled;
        public bool RejectTagCalled;
        public Task ProcessAttachmentAsync(int attachmentId) => Task.CompletedTask;
        public Task ApproveExtractedTagAsync(int extractedTagId, int equipmentId, int? reviewedById = null) { ApproveTagCalled = true; return Task.CompletedTask; }
        public Task RejectExtractedTagAsync(int extractedTagId, string? comment = null, int? reviewedById = null) { RejectTagCalled = true; return Task.CompletedTask; }
        public Task ApproveExtractedSpecificationAsync(int extractedSpecificationId, int? reviewedById = null) => Task.CompletedTask;
        public Task RejectExtractedSpecificationAsync(int extractedSpecificationId, string? comment = null, int? reviewedById = null) => Task.CompletedTask;
    }

    private class FakeTableExtractionImportService : ITableExtractionImportService
    {
        public bool ConfirmRowCalled;
        public bool BulkConfirmCalled;
        public Task<int> StageAsync(int attachmentId, string extractionMethod, List<TableExtractionRowInput> rows, int? createdById) => Task.FromResult(1);
        public Task ConfirmRowAsync(int rowId, int equipmentId, int? reviewedById) { ConfirmRowCalled = true; return Task.CompletedTask; }
        public Task RejectRowAsync(int rowId, int? reviewedById, string? comment) => Task.CompletedTask;
        public Task<(int Confirmed, List<string> Errors)> BulkConfirmMatchedAsync(List<int> rowIds, int? reviewedById)
        { BulkConfirmCalled = true; return Task.FromResult<(int, List<string>)>((rowIds.Count, new List<string>())); }
    }

    private class FakeRelationshipService : IRelationshipService
    {
        public bool ConfirmSuggestedLinkCalled;
        public bool BulkConfirmCalled;
        public Task<RelationshipExplorationDto?> ExploreFromEquipmentAsync(int equipmentId) => Task.FromResult<RelationshipExplorationDto?>(null);
        public Task<RelationshipExplorationDto?> ExploreFromDocumentAsync(int documentId) => Task.FromResult<RelationshipExplorationDto?>(null);
        public Task<RelationshipExplorationDto?> ExploreFromVendorAsync(int vendorId) => Task.FromResult<RelationshipExplorationDto?>(null);
        public Task<RelationshipExplorationDto?> ExploreFromProcurementPackageAsync(int procurementPackageId) => Task.FromResult<RelationshipExplorationDto?>(null);
        public Task<RelationshipExplorationDto?> ExploreFromContractAsync(int contractId) => Task.FromResult<RelationshipExplorationDto?>(null);
        public Task<RelationshipExplorationDto?> ExploreByFreeTextAsync(string text) => Task.FromResult<RelationshipExplorationDto?>(null);
        public Task AddAIProposedRelationshipAsync(string sourceType, int sourceId, string targetType, int targetId, string relationshipType, double confidence) => Task.CompletedTask;
        public Task ConfirmSuggestedLinkAsync(int equipmentId, string targetType, int targetId, bool force = false) { ConfirmSuggestedLinkCalled = true; return Task.CompletedTask; }
        public Task<RelationshipGapReportDto> GetGapReportAsync(int? projectId = null) => Task.FromResult(new RelationshipGapReportDto());
        public Task<List<BulkSuggestedLinkItemDto>> GetAllSuggestedLinksAsync(int? projectId = null) => Task.FromResult(new List<BulkSuggestedLinkItemDto>());
        public Task<(int Confirmed, List<string> Errors)> BulkConfirmAsync(IReadOnlyList<(int EquipmentId, string TargetType, int TargetId)> items)
        { BulkConfirmCalled = true; return Task.FromResult<(int, List<string>)>((items.Count, new List<string>())); }
    }

    private class FakeEngineeringDocumentIntelligenceService : IEngineeringDocumentIntelligenceService
    {
        public bool ApproveClassificationCalled;
        public bool ApproveTagLinkCalled;
        public bool AdvanceStageCalled;
        public Task<DocumentIntelligenceDashboardDto> GetDashboardAsync() => Task.FromResult(new DocumentIntelligenceDashboardDto());
        public Task<List<DocumentIntelligenceDetailDto>> GetPendingReviewAsync() => Task.FromResult(new List<DocumentIntelligenceDetailDto>());
        public Task<DocumentIntelligenceDetailDto?> GetDetailAsync(int documentId) => Task.FromResult<DocumentIntelligenceDetailDto?>(null);
        public Task ApproveClassificationAsync(int classificationId, bool applyDiscipline, bool applyVendor, bool applyMR, bool applyRevision, bool applyIssuePurpose, int? reviewedById = null)
        { ApproveClassificationCalled = true; return Task.CompletedTask; }
        public Task RejectClassificationAsync(int classificationId, int? reviewedById = null) => Task.CompletedTask;
        public Task ApproveTagLinkAsync(int tagLinkId, int? reviewedById = null) { ApproveTagLinkCalled = true; return Task.CompletedTask; }
        public Task RejectTagLinkAsync(int tagLinkId, int? reviewedById = null) => Task.CompletedTask;
        public Task AdvanceStageAsync(int documentId, string newStage) { AdvanceStageCalled = true; return Task.CompletedTask; }
        public Task<int> AddCommentAsync(int documentId, string commentText, string? raisedByName) => Task.FromResult(1);
        public Task SetCommentActionStatusAsync(int commentId, string actionStatus) => Task.CompletedTask;
        public Task RecordCommentResponseAsync(int commentId, string responseText) => Task.CompletedTask;
    }

    // ================= Relationship =================



    // ConfirmSuggestedLink is the operation Relationship.ConfirmSuggestedLink (permission Relationship.Approve, SCRUM-745): the operation host refuses a role without it; the operation calls the service.
    [Fact]
    public async Task Relationship_ConfirmSuggestedLinkOperation_Confirms()
    {
        var service = new FakeRelationshipService();
        var ops = new PEMS.Web.Services.ReliabilityRegisterOperations(null!, null!, service, new FakeCurrentUserService { UserName = "tester" });

        var result = await ops.ConfirmSuggestedLinkAsync(1, "MR", 1);

        Assert.True(result.Success);
        Assert.True(service.ConfirmSuggestedLinkCalled);
    }

    // SCRUM-824: BulkConfirm is an operation (permission Relationship.Approve) over a list of text values; only well formed items reach the service.
    [Fact]
    public void Relationship_BulkConfirm_NeedsTheApprovePermission()
        => Assert.Equal("Relationship.Approve", ((PEMS.Application.Common.PemsOperationAttribute)typeof(PEMS.Application.Interfaces.IRelationshipPagesOperations).GetMethod("BulkConfirmAsync")!
            .GetCustomAttributes(typeof(PEMS.Application.Common.PemsOperationAttribute), false)[0]).Permission);

    [Fact]
    public async Task Relationship_BulkConfirm_ConfirmsTheParsedItems_AndRefusesAnEmptyList()
    {
        var service = new FakeRelationshipService();
        var ops = new PEMS.Web.Services.RelationshipPagesOperations(service);

        Assert.False((await ops.BulkConfirmAsync(new List<string>())).Success);
        Assert.False(service.BulkConfirmCalled);
        Assert.True((await ops.BulkConfirmAsync(new List<string> { "1|MR|1", "bad" })).Success);
        Assert.True(service.BulkConfirmCalled);
    }

    // SCRUM-745: the explorer and the gap report are operations with the permission Relationship.View (the data host refuses a caller without it).
    [Theory]
    [InlineData("ExploreAsync")]
    [InlineData("GapReportAsync")]
    public void Relationship_Reads_NeedTheViewPermission(string method)
        => Assert.Equal("Relationship.View", ((PEMS.Application.Common.PemsOperationAttribute)typeof(PEMS.Application.Interfaces.IRelationshipPagesOperations).GetMethod(method)!
            .GetCustomAttributes(typeof(PEMS.Application.Common.PemsOperationAttribute), false)[0]).Permission);

    [Fact]
    public async Task Relationship_Explore_WithoutAStartPoint_ReturnsNothing()
        => Assert.Null(await new PEMS.Web.Services.RelationshipPagesOperations(new FakeRelationshipService()).ExploreAsync("search", null, " "));

    // ================= OCRProcessing =================
    // طبق ممیزی: OCRProcessingController.Retry مستقیم Hangfire.BackgroundJob.Enqueue (استاتیک، به
    // JobStorage.Current وابسته) را صدا می‌زند - در تست واحد بدون Storage واقعی قابل‌فراخوانی
    // نیست. مسیر «رد بدون مجوز» کاملاً امن تست می‌شود (Forbid قبل از رسیدن به Enqueue برمی‌گردد،
    // پس هیچ Job ای صف نمی‌شود). مسیر «مجاز» بدون واداشتن Hangfire به اجرا، مستقیماً از طریق خودِ
    // IPermissionService تایید می‌شود - اثبات می‌کند نقش‌های مجاز واقعاً از گیت رد می‌شوند.

    [Theory]
    [InlineData(SystemRoles.EngineeringManager)]
    [InlineData(SystemRoles.DocumentController)]
    public async Task OCR_Retry_AuthorizedRoles_WouldPassTheGate(string role)
    {
        var (_, perms, _) = await CreateInfra(nameof(OCR_Retry_AuthorizedRoles_WouldPassTheGate) + role, role);

        var allowed = await perms.HasPermissionAsync(new List<string> { role }, "OCRProcessing", "Create");

        Assert.True(allowed);
    }
}
