using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق ممیزی Package Procurement Contract Finalization (§D/§I قاعده‌ی ۱۱، Phase 5.4/9.4): گیت
/// بازرسی الزامی روی Closeout - پیش از این هیچ تست مستقلی این منطق تازه‌اضافه‌شده را نمی‌آزمود.
/// </summary>
public class ProcurementPackageInspectionGateTests
{
    private static async Task<(PEMS.Persistence.Context.PEMSDbContext Ctx, ProcurementPackageService Service, int PackageId)> CreateAsync(string dbName, bool requiresInspection)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"PkgInspGate_{dbName}");
        var project = new Project { ProjectCode = "P1", ProjectName = "Project One", IsActive = true };
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        var package = new ProcurementPackage
        {
            PackageNumber = "PP-001",
            ProjectId = project.ProjectId,
            Title = "Critical Pumps Package",
            IsActive = true,
            RequiresInspection = requiresInspection
        };
        ctx.ProcurementPackages.Add(package);
        await ctx.SaveChangesAsync();

        var service = new ProcurementPackageService(ctx);
        return (ctx, service, package.ProcurementPackageId);
    }

    [Fact]
    public async Task Closeout_RequiresInspection_WithNoLinkedItp_ThrowsAndDoesNotClose()
    {
        var (ctx, service, packageId) = await CreateAsync(nameof(Closeout_RequiresInspection_WithNoLinkedItp_ThrowsAndDoesNotClose), requiresInspection: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseoutAsync(packageId));

        Assert.NotEqual("Closed", ctx.ProcurementPackages.Single().Status);
    }

    [Fact]
    public async Task Closeout_RequiresInspection_WithOpenItp_ThrowsAndDoesNotClose()
    {
        var (ctx, service, packageId) = await CreateAsync(nameof(Closeout_RequiresInspection_WithOpenItp_ThrowsAndDoesNotClose), requiresInspection: true);
        ctx.InspectionTestPlans.Add(new InspectionTestPlan
        {
            ITPNumber = "ITP-001",
            ProjectId = 1,
            ProcurementPackageId = packageId,
            QCDisciplineId = 1,
            Title = "Pump Inspection",
            Status = "Active"
        });
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseoutAsync(packageId));

        Assert.NotEqual("Closed", ctx.ProcurementPackages.Single().Status);
    }

    [Fact]
    public async Task Closeout_RequiresInspection_WithAllItpsClosed_Succeeds()
    {
        var (ctx, service, packageId) = await CreateAsync(nameof(Closeout_RequiresInspection_WithAllItpsClosed_Succeeds), requiresInspection: true);
        ctx.InspectionTestPlans.Add(new InspectionTestPlan
        {
            ITPNumber = "ITP-001",
            ProjectId = 1,
            ProcurementPackageId = packageId,
            QCDisciplineId = 1,
            Title = "Pump Inspection",
            Status = "Closed"
        });
        await ctx.SaveChangesAsync();

        await service.CloseoutAsync(packageId);

        Assert.Equal("Closed", ctx.ProcurementPackages.Single().Status);
    }

    [Fact]
    public async Task Closeout_DoesNotRequireInspection_SucceedsWithNoItp()
    {
        var (ctx, service, packageId) = await CreateAsync(nameof(Closeout_DoesNotRequireInspection_SucceedsWithNoItp), requiresInspection: false);

        await service.CloseoutAsync(packageId);

        Assert.Equal("Closed", ctx.ProcurementPackages.Single().Status);
    }
}
