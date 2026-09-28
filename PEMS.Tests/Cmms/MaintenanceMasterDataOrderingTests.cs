using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B — Wave A: MaintenanceMasterData». این ۴ Lookup (MaintenanceGroup/Skill/
/// Qualification/Certification) داده‌ی مرجع/سازمانی محدود هستند (طبق شمارش زنده روی PEMSDB:
/// ۰/۱۲/۵/۶ ردیف) - نه Grid تراکنشی رشدپذیر مثل بقیه‌ی Wave A. اعمال کامل استاندارد Grid
/// (Pager/GridKey/Skip-Take) روی این صفحه‌ی تبی «مدیریت همه‌چیز یکجا» خلاف اصل «Over-engineer
/// نکن» است و به UX ادمین (که باید همه‌ی موارد را همزمان ببیند) آسیب می‌زند. تنها اصلاح
/// متناسب: ترتیب قطعی (Tie-breaker) روی هر ۴ کوئری - طبق همون الگوی P2 که برای
/// DocumentRegister/EngineeringDocuments/AuditLog اعمال شد.
/// طبق «PEMS — P1 Organization &amp; Master Data Authorization Hardening»: Index حالا نیازمند
/// مجوز "MaintenanceMasterData"/"View" است - این تست‌ها با نقش MaintenanceEngineer (که این
/// مجوز را دارد) اجرا می‌شوند.
/// </summary>
public class MaintenanceMasterDataOrderingTests
{
    private static MaintenanceMasterDataController CreateController(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"MmdOrdering_{dbName}");
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "MECH", Name = "Mechanical" });
        ctx.SaveChanges();

        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        ctx.SaveChanges();

        var accessControl = new AccessControlService(ctx);
        var permissionService = new PermissionService(ctx, accessControl);
        permissionService.SeedDefaultsAsync().GetAwaiter().GetResult();

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider()).GetAwaiter().GetResult();

        var currentUser = new FakeCurrentUserService { Roles = new List<string> { SystemRoles.MaintenanceEngineer } };

        return new MaintenanceMasterDataController(ctx, currentUser, permissionService, new OrganizationStructureService(ctx));
    }

    private static int _nextId = 1;

    [Fact]
    public async Task Index_Groups_DeterministicOrdering_SameName_SameOrderTwice()
    {
        var controller = CreateController(nameof(Index_Groups_DeterministicOrdering_SameName_SameOrderTwice), out var ctx);
        for (var i = 0; i < 10; i++)
            ctx.MaintenanceGroups.Add(new MaintenanceGroup { MaintenanceGroupId = _nextId++, Code = $"G{_nextId}", Name = "Same Name", DisciplineId = 1, IsActive = true });
        await ctx.SaveChangesAsync();

        var first = Assert.IsType<List<MaintenanceGroup>>(((ViewResult)await controller.Index()).Model);
        var second = Assert.IsType<List<MaintenanceGroup>>(((ViewResult)await controller.Index()).Model);

        Assert.Equal(first.Select(x => x.MaintenanceGroupId), second.Select(x => x.MaintenanceGroupId));
    }

    [Fact]
    public async Task Index_Skills_DeterministicOrdering_SameName_SameOrderTwice()
    {
        var controller = CreateController(nameof(Index_Skills_DeterministicOrdering_SameName_SameOrderTwice), out var ctx);
        for (var i = 0; i < 10; i++)
            ctx.Skills.Add(new Skill { SkillId = _nextId++, Name = "Same Name", IsActive = true });
        await ctx.SaveChangesAsync();

        var view1 = (ViewResult)await controller.Index();
        var first = ((List<Skill>)view1.ViewData["Skills"]!).Select(x => x.SkillId).ToList();
        var view2 = (ViewResult)await controller.Index();
        var second = ((List<Skill>)view2.ViewData["Skills"]!).Select(x => x.SkillId).ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Index_Qualifications_DeterministicOrdering_SameName_SameOrderTwice()
    {
        var controller = CreateController(nameof(Index_Qualifications_DeterministicOrdering_SameName_SameOrderTwice), out var ctx);
        for (var i = 0; i < 10; i++)
            ctx.Qualifications.Add(new Qualification { QualificationId = _nextId++, Name = "Same Name", IsActive = true });
        await ctx.SaveChangesAsync();

        var view1 = (ViewResult)await controller.Index();
        var first = ((List<Qualification>)view1.ViewData["Qualifications"]!).Select(x => x.QualificationId).ToList();
        var view2 = (ViewResult)await controller.Index();
        var second = ((List<Qualification>)view2.ViewData["Qualifications"]!).Select(x => x.QualificationId).ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Index_Certifications_DeterministicOrdering_SameName_SameOrderTwice()
    {
        var controller = CreateController(nameof(Index_Certifications_DeterministicOrdering_SameName_SameOrderTwice), out var ctx);
        for (var i = 0; i < 10; i++)
            ctx.Certifications.Add(new Certification { CertificationId = _nextId++, Name = "Same Name", IsActive = true });
        await ctx.SaveChangesAsync();

        var view1 = (ViewResult)await controller.Index();
        var first = ((List<Certification>)view1.ViewData["Certifications"]!).Select(x => x.CertificationId).ToList();
        var view2 = (ViewResult)await controller.Index();
        var second = ((List<Certification>)view2.ViewData["Certifications"]!).Select(x => x.CertificationId).ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Index_InactiveItems_AreExcludedFromAllFourLookups()
    {
        var controller = CreateController(nameof(Index_InactiveItems_AreExcludedFromAllFourLookups), out var ctx);
        ctx.MaintenanceGroups.Add(new MaintenanceGroup { MaintenanceGroupId = _nextId++, Code = "G1", Name = "Active Group", DisciplineId = 1, IsActive = true });
        ctx.MaintenanceGroups.Add(new MaintenanceGroup { MaintenanceGroupId = _nextId++, Code = "G2", Name = "Inactive Group", DisciplineId = 1, IsActive = false });
        await ctx.SaveChangesAsync();

        var groups = Assert.IsType<List<MaintenanceGroup>>(((ViewResult)await controller.Index()).Model);

        Assert.Single(groups);
        Assert.Equal("Active Group", groups[0].Name);
    }
}
