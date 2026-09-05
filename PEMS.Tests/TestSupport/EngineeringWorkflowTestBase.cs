using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Tests.TestSupport;

/// <summary>
/// طبق Phase E-02: زیرساخت مشترک برای تست‌های گردش‌کار مدارک مهندسی - یک PEMSDbContext
/// InMemory، تعریف گردش‌کار واقعی (EngineeringDocumentWorkflowSeeder، نه یک نسخه‌ی ساده‌شده‌ی
/// جدا) و یک DocumentWorkflowService واقعی روی موتور WorkflowRuntimeService واقعی. تنها بخش
/// Fake شده، ILetterService است (طبق توضیح FakeLetterService).
/// </summary>
public abstract class EngineeringWorkflowTestBase
{
    protected PEMSDbContext Ctx { get; }
    protected DocumentWorkflowService WorkflowService { get; }
    protected FakeLetterService Letters { get; }
    protected Discipline DisciplineMech { get; }
    protected Discipline DisciplineElec { get; }
    protected Project TestProject { get; }

    protected EngineeringWorkflowTestBase()
    {
        Ctx = PemsDbContextFactory.CreateInMemory(Guid.NewGuid().ToString());

        TestProject = new Project { ProjectName = "Test Project", ProjectCode = "TP" };
        DisciplineMech = new Discipline { Name = "Mechanical", Code = "MECH" };
        DisciplineElec = new Discipline { Name = "Electrical", Code = "ELEC" };
        Ctx.Projects.Add(TestProject);
        Ctx.Disciplines.AddRange(DisciplineMech, DisciplineElec);
        Ctx.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(Ctx);
        var provider = services.BuildServiceProvider();
        EngineeringDocumentWorkflowSeeder.SeedAsync(provider).GetAwaiter().GetResult();
        SeedAccessControlPermissions();

        var runtime = new WorkflowRuntimeService(Ctx);
        Letters = new FakeLetterService();
        WorkflowService = new DocumentWorkflowService(Ctx, runtime, Letters, new AccessControlService(Ctx));
    }

    /// <summary>
    /// طبق فاز ۳ RBAC-ABAC: DocumentWorkflowService.RecordClientResponseAsync دیگر آرایه‌ی ثابت
    /// ClientResponseRoles را چک نمی‌کند، بلکه مجوز داده‌محور «Document.RecordClientResponse» را
    /// از طریق AccessControlService/RolePermissionGrant می‌خواند. این فیکسچر ایزوله (InMemory،
    /// بدون Program.cs واقعی) هرگز AccessControlSeeder.SeedPhase3PermissionsAsync را صدا نمی‌زند
    /// (چون آن Seeder روی AspNetRoles واقعی کار می‌کند که این‌جا هم وجود ندارد) - پس همان Grant را
    /// این‌جا عیناً (همان دو نقش: EngineeringManager, DocumentController) بازتولید می‌کنیم تا رفتار
    /// تولید واقعی درست تست شود، نه این‌که مجوز ساختگی سست‌تری جایگزین آن شود.
    /// </summary>
    private void SeedAccessControlPermissions()
    {
        var permission = new Permission
        {
            Name = "Document.RecordClientResponse",
            ResourceName = "Document",
            ActionName = "RecordClientResponse",
            IsDestructive = true,
            IsActive = true
        };
        Ctx.Permissions.Add(permission);
        Ctx.SaveChanges();

        foreach (var roleName in new[] { SystemRoles.EngineeringManager, SystemRoles.DocumentController })
        {
            var role = new PEMS.Persistence.Identity.ApplicationRole { Name = roleName, NormalizedName = roleName.ToUpperInvariant() };
            Ctx.Roles.Add(role);
            Ctx.SaveChanges();

            Ctx.RolePermissionGrants.Add(new RolePermissionGrant
            {
                RoleId = role.Id,
                PermissionId = permission.PermissionId,
                IsActive = true
            });
        }
        Ctx.SaveChanges();
    }

    protected EngineeringDocument CreateDraftDocument(int? disciplineId = null)
    {
        var doc = new EngineeringDocument
        {
            DocumentNo = $"TP-MECH-DAT-{Guid.NewGuid():N}".Substring(0, 20),
            Title = "Test Document",
            DocumentType = "Datasheet",
            ProjectId = TestProject.ProjectId,
            DisciplineId = disciplineId,
            Status = "Draft"
        };

        Ctx.EngineeringDocuments.Add(doc);
        Ctx.SaveChanges();
        return doc;
    }

    protected static readonly string[] ManagerRole = { SystemRoles.EngineeringManager };
    protected static readonly string[] SupervisorRole = { SystemRoles.EngineeringSupervisor };
    protected static readonly string[] DisciplineEngineerRole = { SystemRoles.DisciplineEngineer };
    protected static readonly string[] DocControllerRole = { SystemRoles.DocumentController };
}
