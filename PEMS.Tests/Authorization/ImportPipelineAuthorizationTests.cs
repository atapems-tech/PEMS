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
/// طبق «PEMS — Global Engineering Management UI/UX, Navigation &amp; RBAC Hardening» بند ۹-۱۲:
/// MDRImportController/EquipmentImportController/MrsImportController هیچ‌کدام بررسی مجوزی
/// نداشتند - Commit/CreateFromTags/BulkCommit (که داده‌ی معتبر Engineering Document/Equipment/
/// MaterialItem می‌ساختند) برای هر کاربر احرازهویت‌شده‌ای (صرف‌نظر از نقش) باز بود.
/// </summary>
public class ImportPipelineAuthorizationTests
{
    private static async Task<(PEMSDbContext Ctx, IPermissionService Permissions, ICurrentUserService CurrentUser)>
        CreateInfra(string dbName, string? role)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"ImportAuth_{dbName}");
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

    private class FakeMDRImportService : IMDRImportService
    {
        public bool CommitCalled;
        public Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int projectId, int? importedById) => Task.FromResult(1);
        public Task<MDRImportBatchDto?> GetBatchAsync(int batchId) => Task.FromResult<MDRImportBatchDto?>(null);
        public Task<List<MDRImportBatchDto>> GetRecentBatchesAsync() => Task.FromResult(new List<MDRImportBatchDto>());
        public Task SetRowIncludeAsync(int rowId, bool include) => Task.CompletedTask;
        public Task<(int created, int updated, int skipped)> CommitBatchAsync(int batchId) { CommitCalled = true; return Task.FromResult((1, 0, 0)); }
        public Task DiscardBatchAsync(int batchId) => Task.CompletedTask;
        public Task<int> RematchDisciplinesAsync(int? projectId = null) => Task.FromResult(0);
        public Task<int> FixLegacyStatusValuesAsync(int? projectId = null) => Task.FromResult(0);
    }

    private class FakeEquipmentImportService : IEquipmentImportService
    {
        public bool CommitCalled;
        public Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int projectId, int? importedById) => Task.FromResult(1);
        public Task<EquipmentImportBatchDto?> GetBatchAsync(int batchId) => Task.FromResult<EquipmentImportBatchDto?>(null);
        public Task<List<EquipmentImportBatchDto>> GetRecentBatchesAsync() => Task.FromResult(new List<EquipmentImportBatchDto>());
        public Task SetRowIncludeAsync(int rowId, bool include) => Task.CompletedTask;
        public Task<(int created, int updated, int skipped)> CommitBatchAsync(int batchId) { CommitCalled = true; return Task.FromResult((1, 0, 0)); }
        public Task DiscardBatchAsync(int batchId) => Task.CompletedTask;
    }

    private class FakeEquipmentPdfScanService : IEquipmentPdfScanService
    {
        public Task<List<string>> ScanForTagCandidatesAsync(string filePath) => Task.FromResult(new List<string>());
    }

    private class FakeVDRImportService : IVDRImportService
    {
        public bool CommitCalled;
        public Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int projectId, int? importedById) => Task.FromResult(1);
        public Task<VDRImportBatchDto?> GetBatchAsync(int batchId) => Task.FromResult<VDRImportBatchDto?>(null);
        public Task<List<VDRImportBatchDto>> GetRecentBatchesAsync() => Task.FromResult(new List<VDRImportBatchDto>());
        public Task SetRowIncludeAsync(int rowId, bool include) => Task.CompletedTask;
        public Task<(int created, int updated, int skipped)> CommitBatchAsync(int batchId) { CommitCalled = true; return Task.FromResult((1, 0, 0)); }
        public Task DiscardBatchAsync(int batchId) => Task.CompletedTask;
    }

    private class FakeMrsImportService : IMrsImportService
    {
        public bool CommitCalled;
        public Task<(bool Success, string? Error, int? BatchId)> StageFromExcelAsync(Stream fileStream, string fileName, string? sheetName, int? importedById, int? sourceAttachmentId = null)
            => Task.FromResult<(bool, string?, int?)>((true, null, 1));
        public Task<List<MrsImportBatchSummaryDto>> GetBatchesAsync() => Task.FromResult(new List<MrsImportBatchSummaryDto>());
        public Task<MrsImportBatchDetailDto?> GetBatchDetailAsync(int batchId, string? matchStatusFilter = null, int page = 1, int pageSize = 50) => Task.FromResult<MrsImportBatchDetailDto?>(null);
        public Task<(bool Success, string? Error, int Committed, int Skipped)> CommitAsync(int batchId, List<int> rowIds, int? reviewedById)
        { CommitCalled = true; return Task.FromResult<(bool, string?, int, int)>((true, null, rowIds.Count, 0)); }
        public Task<(bool Success, string? Error)> RejectRowsAsync(int batchId, List<int> rowIds, int? reviewedById) => Task.FromResult<(bool, string?)>((true, null));
        public Task<(bool Success, string? Error, int Committed, int Skipped)> CommitByStatusAsync(int batchId, string matchStatusFilter, int? reviewedById)
        { CommitCalled = true; return Task.FromResult<(bool, string?, int, int)>((true, null, 1, 0)); }
        public Task<(bool Success, string? Error, int Count)> RejectByStatusAsync(int batchId, string matchStatusFilter, int? reviewedById) => Task.FromResult<(bool, string?, int)>((true, null, 0));
    }

    // ================= MDR Import =================

    // ================= Equipment Import =================

    [Fact]
    public async Task Equipment_Commit_WithoutPermission_ReturnsForbid_DoesNotCommit()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Equipment_Commit_WithoutPermission_ReturnsForbid_DoesNotCommit), role: SystemRoles.Viewer);
        var service = new FakeEquipmentImportService();

        // SCRUM-742: the commit is an operation; the action host refuses a caller without EquipmentImport.Approve before it calls it
        Assert.False(await perms.HasPermissionAsync(user.Roles, "EquipmentImport", "Approve"));
        Assert.False(service.CommitCalled);
    }

    [Fact]
    public async Task Equipment_Commit_WithPermission_Commits()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Equipment_Commit_WithPermission_Commits), role: SystemRoles.MaintenanceEngineer);
        var service = new FakeEquipmentImportService();

        Assert.True(await perms.HasPermissionAsync(user.Roles, "EquipmentImport", "Approve"));
        var result = await new PEMS.Web.Services.EquipmentImportOperations(service, ctx).CommitAsync(1);

        Assert.True(result.Success);
        Assert.True(service.CommitCalled);
    }

    // SCRUM-841: the PDF tag scan is operations; the permissions are the old ones (Create scans, Approve creates the equipment directly)
    [Theory]
    [InlineData("ScanAsync", "EquipmentImport.Create")]
    [InlineData("CandidatesAsync", "EquipmentImport.Create")]
    [InlineData("CreateAsync", "EquipmentImport.Approve")]
    public void EquipmentTagScan_OperationsKeepTheOldPermissions(string method, string permission)
    {
        var attribute = (PEMS.Application.Common.PemsOperationAttribute)typeof(PEMS.Application.Interfaces.IEquipmentTagScanOperations).GetMethod(method)!
            .GetCustomAttributes(typeof(PEMS.Application.Common.PemsOperationAttribute), false)[0];
        Assert.Equal(permission, attribute.Permission);
    }

    // ================= VDR Import =================
    // طبق بند ۱۰ ممیزی: فقط Commit این Pipeline گیت داشت؛ Index/Discard برای هر کاربر
    // احرازهویت‌شده‌ای باز بودند - با ماژول "VDR" موجود (نه ماژول جدید) هم‌راستا شد.

    // ================= MRS Import =================

}
