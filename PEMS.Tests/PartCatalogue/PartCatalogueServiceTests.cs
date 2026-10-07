using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Persistence.Services.SearchProviders;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.PartCatalogue;

/// <summary>گردش‌کار کاتالوگ قطعه روی InMemory: Submit/Approve/Return/Cancel، SoD، گیرنده‌ها، Seed ایدمپوتنت.</summary>
public class PartCatalogueServiceTests
{
    private const int Requester = 1, DirectMgr = 2, Superior = 3, Pool = 10, Outsider = 20;

    private sealed class FakeActions : IRoleModuleActionService
    {
        public HashSet<string> Allowed { get; } = new() { "Create", "Review", "Approve", "Admin", "View" };
        public Task<bool> HasActionAsync(IEnumerable<string> userRoles, string module, string action) =>
            Task.FromResult(module == "PartCatalogue"
                && ((userRoles.Contains("PC") && Allowed.Contains(action)) || (userRoles.Contains("PCV") && action == "View")));
        public Task SeedDefaultsAsync() => Task.CompletedTask;
    }

    private sealed class FakeNotifications : INotificationService
    {
        public List<(int User, string Title)> Sent { get; } = new();
        public List<(int? User, string Type, int Id)> Resolved { get; } = new();
        public Task CreateAsync(int userId, string title, string message, string? linkUrl = null, string? relatedType = null, int? relatedId = null)
        { Sent.Add((userId, title)); return Task.CompletedTask; }
        public Task<List<NotificationDto>> GetForUserAsync(int userId, bool unreadOnly = false) => Task.FromResult(new List<NotificationDto>());
        public Task<PagedResult<NotificationDto>> GetInboxAsync(int userId, string? filter, int page, int pageSize) => throw new NotSupportedException();
        public Task<int> GetUnreadCountAsync(int userId) => Task.FromResult(0);
        public Task<bool> MarkAsReadAsync(int userId, int notificationId) => Task.FromResult(true);
        public Task MarkAllAsReadAsync(int userId) => Task.CompletedTask;
        public Task<string?> OpenAsync(int userId, int notificationId) => Task.FromResult<string?>(null);
        public Task<bool> DismissAsync(int userId, int notificationId) => Task.FromResult(true);
        public Task DismissAllAsync(int userId) => Task.CompletedTask;
        public Task MarkRelatedReadAsync(int userId, string relatedType, int relatedId) => Task.CompletedTask;
        public Task ResolveRelatedAsync(int? userId, string relatedType, int relatedId)
        { Resolved.Add((userId, relatedType, relatedId)); return Task.CompletedTask; }
    }

    private sealed class FakeFiles : IFileStorageService
    {
        public Task<string> SaveAsync(string logicalFolder, string originalFileName, Stream content) => Task.FromResult(Guid.NewGuid().ToString("N"));
        public Task<Stream> OpenReadAsync(string key) => Task.FromResult<Stream>(new MemoryStream());
        public Task<bool> ExistsAsync(string key) => Task.FromResult(true);
        public Task DeleteAsync(string key) => Task.CompletedTask;
    }

    private sealed class Env
    {
        public required PEMSDbContext Db { get; init; }
        public required PartCatalogueService Svc { get; init; }
        public required FakeNotifications Notes { get; init; }
        public required FakeActions Actions { get; init; }
        public int Tc, G1, G2, Pf, Uom, Mfr, Grade, PlanB, PlanA;
        public PcActor Actor(int emp, int? user = null) => new(emp, user ?? emp + 100, new[] { "PC" });
    }

    private static async Task<Env> NewEnvAsync()
    {
        var db = PemsDbContextFactory.CreateInMemory("pc_" + Guid.NewGuid().ToString("N"));
        await PartCatalogueSeeder.SeedAsync(db, importExcel: false);

        foreach (var (id, n) in new[] { (1, "Req"), (2, "Direct"), (3, "Superior"), (10, "Pool"), (20, "Outsider") })
            db.Employees.Add(new Employee { EmployeeId = id, FirstName = n, LastName = "T" });

        var tc = new PartCatalogueTechnicalClass { Code = "V-1", Description = "VALVE" };
        var g1 = new PartCatalogueCommodityGroup { Code = "100", Description = "G1" };
        var g2 = new PartCatalogueCommodityGroup { Code = "200", Description = "G2" };
        var pf = new PartCatalogueProductFamily { Code = "PF1", Description = "PF" };
        var uom = new MaterialUOM { Code = "EA", Name = "Each" };
        var mfr = new PartCatalogueManufacturer { Name = "SIEMENS", NormalizedName = "SIEMENS" };
        db.AddRange(tc, g1, g2, pf, uom, mfr);

        db.PartCatalogueOrgLinks.AddRange(
            new PartCatalogueOrgLink { EmployeeId = Requester, ManagerEmployeeId = DirectMgr },
            new PartCatalogueOrgLink { EmployeeId = DirectMgr, ManagerEmployeeId = Superior });
        foreach (var key in new[] { "MaintenanceSupervisor", "SparePartsSpecialist", "SourcingUnit", "CommodityGroupReview", "PartCataloguer", "InventoryControl" })
            db.PartCatalogueApproverAssignments.Add(new PartCatalogueApproverAssignment { StepKey = key, EmployeeId = Pool });
        await db.SaveChangesAsync();

        var notes = new FakeNotifications();
        var actions = new FakeActions();
        var svc = new PartCatalogueService(db, new FakeNumberingService(), notes, new FakeFiles(), actions, new PartCatalogueOptions());
        return new Env
        {
            Db = db, Svc = svc, Notes = notes, Actions = actions,
            Tc = tc.TechnicalClassId, G1 = g1.CommodityGroupId, G2 = g2.CommodityGroupId, Pf = pf.ProductFamilyId,
            Uom = uom.MaterialUOMId, Mfr = mfr.ManufacturerId,
            Grade = db.PartCatalogueStockGrades.First().StockGradeId,
            PlanB = db.PartCataloguePlanningMethods.First(p => p.Code == "B").PlanningMethodId,
            PlanA = db.PartCataloguePlanningMethods.First(p => p.Code == "A").PlanningMethodId
        };
    }

    private static PartCatalogueFormDto Form(Env e, int id, bool planA = false) => new()
    {
        Id = id,
        SectionId = e.Db.PartCatalogueSections.First(s => s.Code == "SERVICES").SectionId,   // بدون بررسی قطعات
        TechnicalClassId = e.Tc, CommodityGroup1Id = e.G1, CommodityGroup2Id = e.G2, ProductFamilyId = e.Pf,
        UnitOfMeasureId = e.Uom, ManufacturerId = e.Mfr, ManufacturerPartNumber = "P-1",
        PlanningMethodId = planA ? e.PlanA : e.PlanB, MinimumStock = planA ? 9 : 2, MaxConsumptionRate = planA ? 9 : 1,
        StockGradeId = e.Grade,
        Attributes = { new PartCatalogueAttributeDto { Name = "FREE FORMAT TEXT", Value = "BALL VALVE" } }
    };

    private static async Task<int> SubmittedAsync(Env e, bool planA = false)
    {
        var id = await e.Svc.CreateDraftAsync("Add", e.Actor(Requester));
        var save = await e.Svc.SaveAsync(Form(e, id, planA), e.Actor(Requester));
        Assert.True(save.Success, string.Join(",", save.Result.Errors.Select(x => x.Key + ":" + x.Field)));
        Assert.True((await e.Svc.GenerateNumberAsync(id, null, e.Actor(Requester))).Success);
        var sub = await e.Svc.SubmitAsync(id, null, e.Actor(Requester));
        Assert.True(sub.Success, string.Join(",", sub.Errors.Select(x => x.Key + ":" + x.Field)));
        return id;
    }

    private static async Task<string?> StatusAsync(Env e, int id) =>
        (await e.Db.PartCatalogueRequests.AsNoTracking().FirstAsync(r => r.PartCatalogueRequestId == id)).Status;

    [Fact]
    public async Task Save_AppliesAutomaticRules_PlanningAZeroesStock_LocalForcesPartNumber()
    {
        var e = await NewEnvAsync();
        var id = await e.Svc.CreateDraftAsync("Add", e.Actor(Requester));
        var form = Form(e, id, planA: true);
        Assert.True((await e.Svc.SaveAsync(form, e.Actor(Requester))).Success);
        var r = await e.Db.PartCatalogueRequests.AsNoTracking().FirstAsync(x => x.PartCatalogueRequestId == id);
        Assert.Equal(0m, r.MinimumStock);
        Assert.Equal(0m, r.MaxConsumptionRate);

        var local = e.Db.PartCatalogueManufacturers.First(m => m.IsLocal);
        form.ManufacturerId = local.ManufacturerId; form.ManufacturerPartNumber = "ignored";
        Assert.True((await e.Svc.SaveAsync(form, e.Actor(Requester))).Success);
        r = await e.Db.PartCatalogueRequests.AsNoTracking().FirstAsync(x => x.PartCatalogueRequestId == id);
        Assert.Equal("NO_PART_NR", r.ManufacturerPartNumber);
    }

    [Fact]
    public async Task Submit_RequiresRequestNumber_AndOnlyOwnerCanSave()
    {
        var e = await NewEnvAsync();
        var id = await e.Svc.CreateDraftAsync("Add", e.Actor(Requester));
        Assert.True((await e.Svc.SaveAsync(Form(e, id), e.Actor(Requester))).Success);

        var noNumber = await e.Svc.SubmitAsync(id, null, e.Actor(Requester));
        Assert.Contains(noNumber.Errors, x => x.Key == "Pc_Err_NumberRequired");

        var other = await e.Svc.SaveAsync(Form(e, id), e.Actor(Outsider));
        Assert.False(other.Success);
    }

    [Fact]
    public async Task Number_IsGeneratedOnlyOnce()
    {
        var e = await NewEnvAsync();
        var id = await e.Svc.CreateDraftAsync("Add", e.Actor(Requester));
        Assert.True((await e.Svc.GenerateNumberAsync(id, null, e.Actor(Requester))).Success);
        var again = await e.Svc.GenerateNumberAsync(id, null, e.Actor(Requester));
        Assert.Contains(again.Errors, x => x.Key == "Pc_Err_NumberExists");
    }

    [Fact]
    public async Task FullChain_ApprovedByRightPeople_CompletesWithPartNumber_AndRequesterCannotApproveOwn()
    {
        var e = await NewEnvAsync();
        var id = await SubmittedAsync(e);

        Assert.Equal("InApproval", await StatusAsync(e, id));
        Assert.False((await e.Svc.ExecuteAsync(id, "Approve", null, null, null, e.Actor(Requester))).Success);   // SoD
        Assert.False((await e.Svc.ExecuteAsync(id, "Approve", null, null, null, e.Actor(Outsider))).Success);   // نه گیرنده، نه Admin ← override نیاز به Admin دارد
        // (Outsider همین نقش PC دارد که Admin هم می‌دهد؛ بنابراین بدون توضیح رد می‌شود)

        Assert.True((await e.Svc.ExecuteAsync(id, "Approve", null, null, null, e.Actor(DirectMgr))).Success);
        Assert.True((await e.Svc.ExecuteAsync(id, "Approve", null, null, null, e.Actor(Superior))).Success);

        // گام‌های Pool: MaintenanceSupervisor (فقط Critical/Capital) و Sourcing/Spare رد شده‌اند ⇒ CommodityGroupReview
        Assert.True((await e.Svc.ExecuteAsync(id, "Approve", null, null, null, e.Actor(Pool))).Success);   // CommodityGroupReview

        // گام ثبت: شماره‌ی قطعه الزامی
        var noPn = await e.Svc.ExecuteAsync(id, "Approve", null, null, null, e.Actor(Pool));
        Assert.Contains(noPn.Errors, x => x.Key == "Pc_Err_PartNumberRequired");

        Assert.True((await e.Svc.ExecuteAsync(id, "Approve", null, "PN-1000", null, e.Actor(Pool))).Success);
        Assert.True((await e.Svc.ExecuteAsync(id, "Approve", null, null, null, e.Actor(Pool))).Success);   // Inventory Control

        Assert.Equal("Completed", await StatusAsync(e, id));
        var r = await e.Db.PartCatalogueRequests.AsNoTracking().FirstAsync(x => x.PartCatalogueRequestId == id);
        Assert.Equal("PN-1000", r.AssignedPartNumber);
        Assert.True(await e.Db.PartCatalogueRequestApprovals.CountAsync(a => a.PartCatalogueRequestId == id) >= 6);  // Submit + ۵ گام فعال
    }

    [Fact]
    public async Task PlanningA_Route_HasNoInventoryControlStep()
    {
        var e = await NewEnvAsync();
        var id = await SubmittedAsync(e, planA: true);
        var step = await e.Db.PartCatalogueRequestSteps.AsNoTracking().FirstAsync(s => s.PartCatalogueRequestId == id && s.StepKey == "InventoryControl");
        Assert.Equal("Skipped", step.Status);
    }

    [Fact]
    public async Task Return_RequiresComment_ThenOwnerCanEditAndResubmit()
    {
        var e = await NewEnvAsync();
        var id = await SubmittedAsync(e);

        var noComment = await e.Svc.ExecuteAsync(id, "Return", null, null, null, e.Actor(DirectMgr));
        Assert.Contains(noComment.Errors, x => x.Key == "Pc_Err_CommentRequired");

        Assert.True((await e.Svc.ExecuteAsync(id, "Return", "fix data", null, null, e.Actor(DirectMgr))).Success);
        Assert.Equal("Returned", await StatusAsync(e, id));

        Assert.True((await e.Svc.SaveAsync(Form(e, id), e.Actor(Requester))).Success);
        Assert.True((await e.Svc.SubmitAsync(id, null, e.Actor(Requester))).Success);
        Assert.Equal("InApproval", await StatusAsync(e, id));
    }

    [Fact]
    public async Task Cancel_ByOthersRequiresComment_AndEndsTheRequest()
    {
        var e = await NewEnvAsync();
        var id = await SubmittedAsync(e);
        // لغو توسط شخص دیگر (غیر از درخواست‌دهنده) بدون توضیح پذیرفته نمی‌شود؛ درخواست‌دهنده توضیح اختیاری دارد
        Assert.False((await e.Svc.ExecuteAsync(id, "Cancel", null, null, null, e.Actor(DirectMgr))).Success);
        Assert.True((await e.Svc.ExecuteAsync(id, "Cancel", "not needed", null, null, e.Actor(Requester))).Success);
        Assert.Equal("Cancelled", await StatusAsync(e, id));
        Assert.False((await e.Svc.ExecuteAsync(id, "Approve", null, null, null, e.Actor(DirectMgr))).Success);
    }

    [Fact]
    public async Task MissingDirectManager_BlocksSubmit()
    {
        var e = await NewEnvAsync();
        e.Db.PartCatalogueOrgLinks.RemoveRange(e.Db.PartCatalogueOrgLinks);
        await e.Db.SaveChangesAsync();

        var id = await e.Svc.CreateDraftAsync("Add", e.Actor(Requester));
        Assert.True((await e.Svc.SaveAsync(Form(e, id), e.Actor(Requester))).Success);
        await e.Svc.GenerateNumberAsync(id, null, e.Actor(Requester));
        var r = await e.Svc.SubmitAsync(id, null, e.Actor(Requester));
        Assert.False(r.Success);
        Assert.Equal("Draft", await StatusAsync(e, id));
    }

    [Fact]
    public async Task WithoutCreatePermission_DraftCannotBeCreated()
    {
        var e = await NewEnvAsync();
        e.Actions.Allowed.Remove("Create");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => e.Svc.CreateDraftAsync("Add", e.Actor(Requester)));
    }

    [Fact]
    public async Task Seeder_IsIdempotent_AndDoesNotOverwriteAdminChanges()
    {
        var e = await NewEnvAsync();
        var steps = await e.Db.PartCatalogueWorkflowSteps.CountAsync();
        var sections = await e.Db.PartCatalogueSections.CountAsync();
        Assert.Equal(8, steps);
        Assert.Equal(23, sections);

        var step = await e.Db.PartCatalogueWorkflowSteps.FirstAsync(s => s.StepKey == "SourcingUnit");
        step.StepOrder = 55; step.IsActive = false;
        await e.Db.SaveChangesAsync();

        await PartCatalogueSeeder.SeedAsync(e.Db, importExcel: false);
        await PartCatalogueSeeder.SeedAsync(e.Db, importExcel: false);

        Assert.Equal(steps, await e.Db.PartCatalogueWorkflowSteps.CountAsync());
        Assert.Equal(sections, await e.Db.PartCatalogueSections.CountAsync());
        var again = await e.Db.PartCatalogueWorkflowSteps.AsNoTracking().FirstAsync(s => s.StepKey == "SourcingUnit");
        Assert.Equal(55, again.StepOrder);
        Assert.False(again.IsActive);
    }

    [Fact]
    public async Task GlobalSearch_ScopesResultsByPermissionAndOwnership()
    {
        var e = await NewEnvAsync();
        var id = await SubmittedAsync(e);
        var number = (await e.Db.PartCatalogueRequests.AsNoTracking().FirstAsync(r => r.PartCatalogueRequestId == id)).RequestNumber!;

        async Task<int> Count(int emp, params string[] roles)
        {
            var user = new FakeCurrentUserContext { UserId = emp + 100, EmployeeId = emp, Roles = roles };
            var provider = new PartCatalogueRequestSearchProvider(e.Db, user, e.Actions);
            return (await provider.SearchAsync(number, 1, 20, null)).TotalCount;
        }

        Assert.Equal(1, await Count(Pool, "PC"));        // Review/Approve/Admin ⇒ همه
        Assert.Equal(0, await Count(Outsider, "NONE"));  // بدون مجوز
        Assert.Equal(1, await Count(Requester, "PCV"));  // View + مالک
        Assert.Equal(0, await Count(Outsider, "PCV"));   // View ولی نه مالک و نه گیرنده
        Assert.Equal(1, await Count(DirectMgr, "PCV"));  // View + گیرنده‌ی گام
    }
}
