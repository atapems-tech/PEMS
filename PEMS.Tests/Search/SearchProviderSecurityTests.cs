using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Persistence.Services.SearchProviders;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Search;

/// <summary>
/// طبق «PEMS Phase 2A — Global Search Security»: این تست‌ها ثابت می‌کنند Providerهایی که در
/// این فاز اصلاح شدند، دقیقاً همون محدودیت‌های صفحه‌ی اصلی موجودیت (LetterService.SearchAsync
/// برای محرمانگی، EngineeringDocumentsController برای دیسیپلین، WorkOrderController/
/// MaterialRequisitionController برای مجوز ماژول) را واقعاً روی خودِ Query دیتابیس اعمال
/// می‌کنند - نه فقط در UI مخفی می‌کنند. الگوی Setup عیناً همون AccessControlParityTests است
/// (داده‌ی Seed واقعی، نه یک Fixture دستی جدا از رفتار واقعی سیستم).
/// </summary>
public class SearchProviderSecurityTests
{
    private static async Task<(PEMSDbContext ctx, AccessControlService accessControl, PermissionService permissionService)>
        SeedAuthorizationAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory(dbName);

        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();

        var accessControl = new AccessControlService(ctx);
        var permissionService = new PermissionService(ctx, accessControl);
        await permissionService.SeedDefaultsAsync();

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        var provider = services.BuildServiceProvider();
        await AccessControlSeeder.SeedFromLegacyAsync(provider);
        await AccessControlSeeder.SeedPhase3PermissionsAsync(provider);

        return (ctx, accessControl, permissionService);
    }

    // ==============================================================================
    // Letter — طبق بند D (Permission + Resource): همون قانون محرمانگی LetterService.SearchAsync
    // ==============================================================================

    [Fact]
    public async Task LetterSearchProvider_UnauthorizedUser_CannotFindConfidentialLetterByNumber()
    {
        var (ctx, _, permissionService) = await SeedAuthorizationAsync(nameof(LetterSearchProvider_UnauthorizedUser_CannotFindConfidentialLetterByNumber));

        ctx.Letters.Add(new Letter
        {
            LetterId = 1, LetterType = "Outgoing", LetterNumber = "SEC-CONF-0001",
            Subject = "Top secret negotiation", ConfidentialityLevel = "Confidential",
            RegisteredById = 999, Status = "Draft", IsActive = true
        });
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUserContext { UserId = 1, EmployeeId = 1, Roles = new List<string> { SystemRoles.DisciplineEngineer } };
        var provider = new LetterSearchProvider(ctx, new FakeKnowledgeBaseService(), permissionService, currentUser);

        var (items, total) = await provider.SearchAsync("SEC-CONF-0001", 1, 20, null);

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task LetterSearchProvider_AuthorizedUser_CanFindConfidentialLetter()
    {
        var (ctx, _, permissionService) = await SeedAuthorizationAsync(nameof(LetterSearchProvider_AuthorizedUser_CanFindConfidentialLetter));

        ctx.Letters.Add(new Letter
        {
            LetterId = 1, LetterType = "Outgoing", LetterNumber = "SEC-CONF-0002",
            Subject = "Top secret negotiation", ConfidentialityLevel = "Confidential",
            RegisteredById = 999, Status = "Draft", IsActive = true
        });
        await ctx.SaveChangesAsync();

        // DocumentController دارای مجوز Correspondence/Approve است طبق Seed واقعی - یعنی مجاز به دیدن محرمانه
        var currentUser = new FakeCurrentUserContext { UserId = 1, EmployeeId = 1, Roles = new List<string> { SystemRoles.DocumentController } };
        var provider = new LetterSearchProvider(ctx, new FakeKnowledgeBaseService(), permissionService, currentUser);

        var (items, total) = await provider.SearchAsync("SEC-CONF-0002", 1, 20, null);

        Assert.Single(items);
        Assert.Equal(1, total);
    }

    [Fact]
    public async Task LetterSearchProvider_UnauthorizedUser_CanFindOwnRegisteredConfidentialLetter()
    {
        var (ctx, _, permissionService) = await SeedAuthorizationAsync(nameof(LetterSearchProvider_UnauthorizedUser_CanFindOwnRegisteredConfidentialLetter));

        ctx.Letters.Add(new Letter
        {
            LetterId = 1, LetterType = "Outgoing", LetterNumber = "SEC-CONF-0003",
            Subject = "My own draft", ConfidentialityLevel = "Confidential",
            RegisteredById = 42, Status = "Draft", IsActive = true
        });
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUserContext { UserId = 42, EmployeeId = 1, Roles = new List<string> { SystemRoles.DisciplineEngineer } };
        var provider = new LetterSearchProvider(ctx, new FakeKnowledgeBaseService(), permissionService, currentUser);

        var (items, _) = await provider.SearchAsync("SEC-CONF-0003", 1, 20, null);

        Assert.Single(items);
    }

    [Fact]
    public async Task LetterSearchProvider_UnauthorizedUser_CanFindConfidentialLetterReferredToThem()
    {
        var (ctx, _, permissionService) = await SeedAuthorizationAsync(nameof(LetterSearchProvider_UnauthorizedUser_CanFindConfidentialLetterReferredToThem));

        ctx.Letters.Add(new Letter
        {
            LetterId = 1, LetterType = "Incoming", LetterNumber = "SEC-CONF-0004",
            Subject = "Referred to me", ConfidentialityLevel = "Restricted",
            RegisteredById = 999, Status = "Registered", IsActive = true
        });
        ctx.LetterReferrals.Add(new LetterReferral { LetterReferralId = 1, LetterId = 1, ReferredToEmployeeId = 7, ReferredDate = DateTime.Now, Status = "Pending" });
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUserContext { UserId = 1, EmployeeId = 7, Roles = new List<string> { SystemRoles.DisciplineEngineer } };
        var provider = new LetterSearchProvider(ctx, new FakeKnowledgeBaseService(), permissionService, currentUser);

        var (items, _) = await provider.SearchAsync("SEC-CONF-0004", 1, 20, null);

        Assert.Single(items);
    }

    [Fact]
    public async Task LetterSearchProvider_UnauthorizedUser_StillFindsPublicLetters_NoRegression()
    {
        var (ctx, _, permissionService) = await SeedAuthorizationAsync(nameof(LetterSearchProvider_UnauthorizedUser_StillFindsPublicLetters_NoRegression));

        ctx.Letters.Add(new Letter
        {
            LetterId = 1, LetterType = "Outgoing", LetterNumber = "SEC-PUB-0001",
            Subject = "Ordinary public letter", ConfidentialityLevel = "Public",
            RegisteredById = 999, Status = "Draft", IsActive = true
        });
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUserContext { UserId = 1, EmployeeId = 1, Roles = new List<string> { SystemRoles.DisciplineEngineer } };
        var provider = new LetterSearchProvider(ctx, new FakeKnowledgeBaseService(), permissionService, currentUser);

        var (items, total) = await provider.SearchAsync("SEC-PUB-0001", 1, 20, null);

        Assert.Single(items);
        Assert.Equal(1, total);
    }

    // ==============================================================================
    // EngineeringDocument / DocumentRevision — طبق بند C (Permission + Resource): همون قانون
    // دیسیپلین EngineeringDocumentsController.ScopeToOwnDisciplineIfRestrictedAsync
    // ==============================================================================

    [Fact]
    public async Task EngineeringDocumentSearchProvider_DisciplineEngineer_CannotFindOtherDisciplineDocument()
    {
        var (ctx, accessControl, _) = await SeedAuthorizationAsync(nameof(EngineeringDocumentSearchProvider_DisciplineEngineer_CannotFindOtherDisciplineDocument));

        var mech = new Discipline { DisciplineId = 1, Name = "Mechanical", Code = "MEC", IsActive = true };
        var elec = new Discipline { DisciplineId = 2, Name = "Electrical", Code = "ELE", IsActive = true };
        ctx.Disciplines.AddRange(mech, elec);
        ctx.Employees.Add(new Employee { EmployeeId = 1, FirstName = "A", LastName = "B", PersonnelCode = "E1", DisciplineId = 1, IsActive = true });
        ctx.Users.Add(new ApplicationUser { Id = 1, UserName = "mech.engineer", EmployeeId = 1, IsActive = true });
        ctx.Set<EngineeringDocument>().Add(new EngineeringDocument
        {
            DocumentId = 1, DocumentNo = "DOC-ELE-00001", Title = "Electrical single line diagram",
            DisciplineId = 2, Status = "Approved", IsActive = true
        });
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUserContext { UserId = 1, EmployeeId = 1, Roles = new List<string> { SystemRoles.DisciplineEngineer } };
        var provider = new EngineeringDocumentSearchProvider(ctx, new FakeKnowledgeBaseService(), accessControl, currentUser);

        var (items, total) = await provider.SearchAsync("DOC-ELE-00001", 1, 20, null);

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task EngineeringDocumentSearchProvider_DisciplineEngineer_CanFindOwnDisciplineDocument()
    {
        var (ctx, accessControl, _) = await SeedAuthorizationAsync(nameof(EngineeringDocumentSearchProvider_DisciplineEngineer_CanFindOwnDisciplineDocument));

        var mech = new Discipline { DisciplineId = 1, Name = "Mechanical", Code = "MEC", IsActive = true };
        ctx.Disciplines.Add(mech);
        ctx.Employees.Add(new Employee { EmployeeId = 1, FirstName = "A", LastName = "B", PersonnelCode = "E1", DisciplineId = 1, IsActive = true });
        ctx.Users.Add(new ApplicationUser { Id = 1, UserName = "mech.engineer", EmployeeId = 1, IsActive = true });
        ctx.Set<EngineeringDocument>().Add(new EngineeringDocument
        {
            DocumentId = 1, DocumentNo = "DOC-MEC-00001", Title = "Pump datasheet",
            DisciplineId = 1, Status = "Approved", IsActive = true
        });
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUserContext { UserId = 1, EmployeeId = 1, Roles = new List<string> { SystemRoles.DisciplineEngineer } };
        var provider = new EngineeringDocumentSearchProvider(ctx, new FakeKnowledgeBaseService(), accessControl, currentUser);

        var (items, _) = await provider.SearchAsync("DOC-MEC-00001", 1, 20, null);

        Assert.Single(items);
    }

    [Fact]
    public async Task EngineeringDocumentSearchProvider_EngineeringManager_CanFindAnyDisciplineDocument()
    {
        var (ctx, accessControl, _) = await SeedAuthorizationAsync(nameof(EngineeringDocumentSearchProvider_EngineeringManager_CanFindAnyDisciplineDocument));

        var elec = new Discipline { DisciplineId = 2, Name = "Electrical", Code = "ELE", IsActive = true };
        ctx.Disciplines.Add(elec);
        ctx.Set<EngineeringDocument>().Add(new EngineeringDocument
        {
            DocumentId = 1, DocumentNo = "DOC-ELE-00002", Title = "Electrical single line diagram",
            DisciplineId = 2, Status = "Approved", IsActive = true
        });
        await ctx.SaveChangesAsync();

        // EngineeringManager دارای مجوز EngineeringDocuments/ViewAllDisciplines است طبق Seed فاز ۳
        var currentUser = new FakeCurrentUserContext { UserId = 2, EmployeeId = null, Roles = new List<string> { SystemRoles.EngineeringManager } };
        var provider = new EngineeringDocumentSearchProvider(ctx, new FakeKnowledgeBaseService(), accessControl, currentUser);

        var (items, _) = await provider.SearchAsync("DOC-ELE-00002", 1, 20, null);

        Assert.Single(items);
    }

    [Fact]
    public async Task DocumentRevisionSearchProvider_DisciplineEngineer_CannotFindOtherDisciplineRevision()
    {
        var (ctx, accessControl, _) = await SeedAuthorizationAsync(nameof(DocumentRevisionSearchProvider_DisciplineEngineer_CannotFindOtherDisciplineRevision));

        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Name = "Mechanical", Code = "MEC", IsActive = true });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 2, Name = "Electrical", Code = "ELE", IsActive = true });
        ctx.Employees.Add(new Employee { EmployeeId = 1, FirstName = "A", LastName = "B", PersonnelCode = "E1", DisciplineId = 1, IsActive = true });
        ctx.Users.Add(new ApplicationUser { Id = 1, UserName = "mech.engineer", EmployeeId = 1, IsActive = true });
        var doc = new EngineeringDocument { DocumentId = 1, DocumentNo = "DOC-ELE-00003", Title = "x", DisciplineId = 2, Status = "Approved", IsActive = true };
        ctx.Set<EngineeringDocument>().Add(doc);
        ctx.DocumentRevisions.Add(new DocumentRevision { RevisionId = 1, DocumentId = 1, RevisionNo = "01", Status = "Approved", RevisionDate = DateTime.Now, IsActive = true });
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUserContext { UserId = 1, EmployeeId = 1, Roles = new List<string> { SystemRoles.DisciplineEngineer } };
        var provider = new DocumentRevisionSearchProvider(ctx, accessControl, currentUser);

        var (items, _) = await provider.SearchAsync("DOC-ELE-00003", 1, 20, null);

        Assert.Empty(items);
    }

    // ==============================================================================
    // WorkOrder / MaterialRequisition — طبق بند A (Permission only): همون مجوز
    // WorkOrderController.Index/MaterialRequisitionController.Index
    // ==============================================================================

    [Fact]
    public async Task WorkOrderSearchProvider_RoleWithoutWorkOrderPermission_FindsNothing()
    {
        var (ctx, _, permissionService) = await SeedAuthorizationAsync(nameof(WorkOrderSearchProvider_RoleWithoutWorkOrderPermission_FindsNothing));

        ctx.WorkOrders.Add(new WorkOrder { WorkOrderId = 1, WorkOrderNumber = "WO-SEC-0001", Description = "x", Status = "Open", Priority = "Medium", IsActive = true });
        await ctx.SaveChangesAsync();

        // طبق Seed واقعی WorkOrder: QAManager هیچ ردیفی در RolePermissions ندارد
        var currentUser = new FakeCurrentUserContext { UserId = 1, Roles = new List<string> { SystemRoles.QAManager } };
        var provider = new WorkOrderSearchProvider(ctx, permissionService, currentUser);

        var (items, total) = await provider.SearchAsync("WO-SEC-0001", 1, 20, null);

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task WorkOrderSearchProvider_RoleWithWorkOrderPermission_FindsResult()
    {
        var (ctx, _, permissionService) = await SeedAuthorizationAsync(nameof(WorkOrderSearchProvider_RoleWithWorkOrderPermission_FindsResult));

        // طبق تشخیص Diagnostic: WorkOrder.EquipmentId یک FK الزامی (int، نه int?) است -
        // Include(x => x.Equipment) روی EF Core InMemory بدون یک ردیف Equipment واقعی، خودِ
        // WorkOrder را هم از نتیجه حذف می‌کند (این رفتار Provider موجود است، نه چیزی که این
        // فاز تغییر داده - پس این‌جا صرفاً داده‌ی تست واقعی‌تر می‌شود، نه تغییر رفتار Provider)
        var equipment = new Equipment { EquipmentId = 1, TagNumber = "EQ-0001", Name = "Pump", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        ctx.WorkOrders.Add(new WorkOrder { WorkOrderId = 1, WorkOrderNumber = "WO-SEC-0002", Description = "x", Status = "Open", Priority = "Medium", EquipmentId = equipment.EquipmentId, IsActive = true });
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUserContext { UserId = 1, Roles = new List<string> { SystemRoles.MaintenanceEngineer } };
        var provider = new WorkOrderSearchProvider(ctx, permissionService, currentUser);

        var (items, _) = await provider.SearchAsync("WO-SEC-0002", 1, 20, null);

        Assert.Single(items);
    }

    [Fact]
    public async Task MaterialRequisitionSearchProvider_ViewerRole_FindsNothing()
    {
        var (ctx, _, permissionService) = await SeedAuthorizationAsync(nameof(MaterialRequisitionSearchProvider_ViewerRole_FindsNothing));

        ctx.MaterialRequisitions.Add(new MaterialRequisition { MRId = 1, MRNumber = "MR-SEC-0001", Description = "x", Status = "Draft" });
        await ctx.SaveChangesAsync();

        // طبق Seed واقعی MaterialRequisition: Viewer هیچ ردیفی ندارد (فقط EngineeringManager/DisciplineEngineer دارند)
        var currentUser = new FakeCurrentUserContext { UserId = 1, Roles = new List<string> { SystemRoles.Viewer } };
        var provider = new MaterialRequisitionSearchProvider(ctx, permissionService, currentUser);

        var (items, total) = await provider.SearchAsync("MR-SEC-0001", 1, 20, null);

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task MaterialRequisitionSearchProvider_DisciplineEngineer_FindsResult()
    {
        var (ctx, _, permissionService) = await SeedAuthorizationAsync(nameof(MaterialRequisitionSearchProvider_DisciplineEngineer_FindsResult));

        // همون نکته‌ی WorkOrder بالا: MaterialRequisition.DisciplineId هم یک FK الزامی است
        var discipline = new Discipline { DisciplineId = 1, Name = "Mechanical", Code = "MEC", IsActive = true };
        ctx.Disciplines.Add(discipline);
        ctx.MaterialRequisitions.Add(new MaterialRequisition { MRId = 1, MRNumber = "MR-SEC-0002", Description = "x", Status = "Draft", DisciplineId = discipline.DisciplineId });
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUserContext { UserId = 1, Roles = new List<string> { SystemRoles.DisciplineEngineer } };
        var provider = new MaterialRequisitionSearchProvider(ctx, permissionService, currentUser);

        var (items, _) = await provider.SearchAsync("MR-SEC-0002", 1, 20, null);

        Assert.Single(items);
    }

    // ==============================================================================
    // Attachment — طبق بند D/C: هر دو قانون (محرمانگی نامه + دیسیپلین مدرک) روی پیوست هم اعمال
    // می‌شود، بسته به این‌که پیوست به کدام والد وصل است
    // ==============================================================================

    [Fact]
    public async Task AttachmentSearchProvider_UnauthorizedUser_CannotFindAttachmentOnConfidentialLetter()
    {
        var (ctx, accessControl, permissionService) = await SeedAuthorizationAsync(nameof(AttachmentSearchProvider_UnauthorizedUser_CannotFindAttachmentOnConfidentialLetter));

        ctx.Letters.Add(new Letter
        {
            LetterId = 1, LetterType = "Outgoing", LetterNumber = "SEC-ATT-0001",
            Subject = "x", ConfidentialityLevel = "Confidential", RegisteredById = 999, Status = "Draft", IsActive = true
        });
        ctx.DocumentAttachments.Add(new DocumentAttachment
        {
            AttachmentId = 1, LetterId = 1, FileName = "secret-negotiation.pdf",
            FileExtension = ".pdf", FilePath = "x", UploadDate = DateTime.Now, IsActive = true
        });
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUserContext { UserId = 1, EmployeeId = 1, Roles = new List<string> { SystemRoles.DisciplineEngineer } };
        var provider = new AttachmentSearchProvider(ctx, accessControl, permissionService, currentUser);

        var (items, total) = await provider.SearchAsync("secret-negotiation", 1, 20, null);

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task AttachmentSearchProvider_AttachmentNotYetLinkedToAnyParent_IsStillFound()
    {
        // طبق طراحی: پیوست حین Intake (پیش از پیوند به مدرک/نامه) نباید توسط این دو قانون
        // بی‌دلیل مسدود شود - فقط وقتی واقعاً به یک والد مشخص وصل است محدودیت اعمال می‌شود
        var (ctx, accessControl, permissionService) = await SeedAuthorizationAsync(nameof(AttachmentSearchProvider_AttachmentNotYetLinkedToAnyParent_IsStillFound));

        ctx.DocumentAttachments.Add(new DocumentAttachment
        {
            AttachmentId = 1, FileName = "unlinked-intake-file.pdf",
            FileExtension = ".pdf", FilePath = "x", UploadDate = DateTime.Now, IsActive = true
        });
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUserContext { UserId = 1, EmployeeId = 1, Roles = new List<string> { SystemRoles.DisciplineEngineer } };
        var provider = new AttachmentSearchProvider(ctx, accessControl, permissionService, currentUser);

        var (items, _) = await provider.SearchAsync("unlinked-intake-file", 1, 20, null);

        Assert.Single(items);
    }

    // ==============================================================================
    // Notification — طبق بند E (User-specific): تایید عدم رگرسیون روی تنها Provider ای که از
    // قبل درست بود
    // ==============================================================================

    [Fact]
    public async Task NotificationSearchProvider_NeverReturnsAnotherUsersNotification()
    {
        var ctx = PemsDbContextFactory.CreateInMemory(nameof(NotificationSearchProvider_NeverReturnsAnotherUsersNotification));
        ctx.Notifications.Add(new Notification { NotificationId = 1, UserId = 999, Title = "Someone else's notification", Message = "x", CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUserContext { UserId = 1 };
        var provider = new NotificationSearchProvider(ctx, currentUser);

        var (items, total) = await provider.SearchAsync("Someone else", 1, 20, null);

        Assert.Empty(items);
        Assert.Equal(0, total);
    }
}
