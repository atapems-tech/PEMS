using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق ممیزی نهایی «PEMS — Final Procurement-to-Contract Ownership, Traceability, UI Alignment &amp;
/// Closure» (§16/§26): پوشش نقاط ورود تازه‌ی Relationship Explorer از ProcurementPackage/Contract
/// و - مهم‌تر - یک تست یکپارچه‌سازی که ثابت کند Contract واقعاً تا ProcurementPackage مبدایش
/// قابل ردیابی است (از طریق Tender.ContractId، تنها منبع حقیقت این رابطه طبق Phase 3).
/// </summary>
public class RelationshipServiceOwnershipTests
{
    private static async Task<(PEMSDbContext Ctx, RelationshipService Service, int PackageId, int TenderId, int VendorId, int BidId, int ContractId)> CreateAwardedChainAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"RelOwnership_{dbName}");

        var project = new Project { ProjectCode = "P1", ProjectName = "Project One", IsActive = true };
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        var package = new ProcurementPackage { PackageNumber = "PP-100", ProjectId = project.ProjectId, Title = "Rotating Equipment", IsActive = true };
        ctx.ProcurementPackages.Add(package);

        var vendor = new Vendor { Name = "Acme Vendor", IsActive = true };
        ctx.Vendors.Add(vendor);
        await ctx.SaveChangesAsync();

        var tender = new Tender { TenderNumber = "TND-100", ProcurementPackageId = package.ProcurementPackageId, Title = "Pumps Tender", Status = "Awarded", IsActive = true };
        ctx.Tenders.Add(tender);
        await ctx.SaveChangesAsync();

        var bid = new Bid { BidNumber = "BID-100", TenderId = tender.TenderId, VendorId = vendor.VendorId, Status = "Won", IsActive = true };
        ctx.Bids.Add(bid);
        await ctx.SaveChangesAsync();

        var contract = new Contract { ContractNo = "CTR-100", ProjectId = project.ProjectId, VendorId = vendor.VendorId, Status = "Effective", IsActive = true };
        ctx.Contracts.Add(contract);
        await ctx.SaveChangesAsync();

        tender.AwardedBidId = bid.BidId;
        tender.ContractId = contract.ContractId;
        await ctx.SaveChangesAsync();

        var ntp = new ContractNTP { ContractId = contract.ContractId, NTPNumber = "NTP-100", Status = "Issued", IsActive = true };
        ctx.ContractNTPs.Add(ntp);

        var kickoff = new Meeting { Title = "Kick-off Meeting", MeetingType = "KickOff", ContractId = contract.ContractId, IsActive = true };
        ctx.Meetings.Add(kickoff);

        var po = new PurchaseOrder { PoNumber = "PO-100", ProjectId = project.ProjectId, VendorId = vendor.VendorId, ContractId = contract.ContractId, IsActive = true };
        ctx.PurchaseOrders.Add(po);

        await ctx.SaveChangesAsync();

        var service = new RelationshipService(ctx);

        return (ctx, service, package.ProcurementPackageId, tender.TenderId, vendor.VendorId, bid.BidId, contract.ContractId);
    }

    [Fact]
    public async Task ExploreFromProcurementPackage_UnknownId_ReturnsNull()
    {
        var ctx = PemsDbContextFactory.CreateInMemory(nameof(ExploreFromProcurementPackage_UnknownId_ReturnsNull));
        var service = new RelationshipService(ctx);

        var result = await service.ExploreFromProcurementPackageAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExploreFromProcurementPackage_ShowsFullChain_ToContractNTPKickoffAndPO()
    {
        var (_, service, packageId, tenderId, _, bidId, contractId) = await CreateAwardedChainAsync(
            nameof(ExploreFromProcurementPackage_ShowsFullChain_ToContractNTPKickoffAndPO));

        var result = await service.ExploreFromProcurementPackageAsync(packageId);

        Assert.NotNull(result);
        Assert.Equal("ProcurementPackage", result!.RootType);
        Assert.Contains(result.Tenders, n => n.NodeId == tenderId);
        Assert.Contains(result.Bids, n => n.NodeId == bidId);
        Assert.Contains(result.Contracts, n => n.NodeId == contractId);
        Assert.Single(result.ContractNTPs);
        Assert.Single(result.KickoffMeetings);
        Assert.Single(result.PurchaseOrders);
    }

    [Fact]
    public async Task ExploreFromContract_UnknownId_ReturnsNull()
    {
        var ctx = PemsDbContextFactory.CreateInMemory(nameof(ExploreFromContract_UnknownId_ReturnsNull));
        var service = new RelationshipService(ctx);

        var result = await service.ExploreFromContractAsync(999);

        Assert.Null(result);
    }

    // طبق §26: حداقل یک تست یکپارچه‌سازی که ثابت کند Contract تا ProcurementPackage مبدایش
    // قابل ردیابی است - این‌جا از سمت Contract شروع می‌کنیم و باید هم Tender و هم Package مبدا
    // در نتیجه دیده شوند.
    [Fact]
    public async Task ExploreFromContract_TracesBackToOriginatingTenderAndProcurementPackage()
    {
        var (_, service, packageId, tenderId, _, _, contractId) = await CreateAwardedChainAsync(
            nameof(ExploreFromContract_TracesBackToOriginatingTenderAndProcurementPackage));

        var result = await service.ExploreFromContractAsync(contractId);

        Assert.NotNull(result);
        Assert.Equal("Contract", result!.RootType);
        Assert.Contains(result.Tenders, n => n.NodeId == tenderId);
        Assert.Contains(result.ProcurementPackages, n => n.NodeId == packageId);
        Assert.Single(result.ContractNTPs);
        Assert.Single(result.KickoffMeetings);
        Assert.Single(result.PurchaseOrders);
    }

    [Fact]
    public async Task ExploreFromContract_WithNoOriginatingTender_StillReturnsRoot_WithEmptyPackageChain()
    {
        var ctx = PemsDbContextFactory.CreateInMemory(nameof(ExploreFromContract_WithNoOriginatingTender_StillReturnsRoot_WithEmptyPackageChain));

        var project = new Project { ProjectCode = "P2", ProjectName = "Project Two", IsActive = true };
        ctx.Projects.Add(project);
        var vendor = new Vendor { Name = "Direct Vendor", IsActive = true };
        ctx.Vendors.Add(vendor);
        await ctx.SaveChangesAsync();

        var contract = new Contract { ContractNo = "CTR-200", ProjectId = project.ProjectId, VendorId = vendor.VendorId, Status = "Draft", IsActive = true };
        ctx.Contracts.Add(contract);
        await ctx.SaveChangesAsync();

        var service = new RelationshipService(ctx);

        var result = await service.ExploreFromContractAsync(contract.ContractId);

        Assert.NotNull(result);
        Assert.Empty(result!.Tenders);
        Assert.Empty(result.ProcurementPackages);
    }

    [Fact]
    public async Task ExploreFromProcurementPackage_ItpLinkedDirectlyToPackage_IsSurfaced()
    {
        var ctx = PemsDbContextFactory.CreateInMemory(nameof(ExploreFromProcurementPackage_ItpLinkedDirectlyToPackage_IsSurfaced));

        var project = new Project { ProjectCode = "P3", ProjectName = "Project Three", IsActive = true };
        ctx.Projects.Add(project);
        var discipline = new QCDiscipline { Name = "Mechanical" };
        ctx.QCDisciplines.Add(discipline);
        await ctx.SaveChangesAsync();

        var package = new ProcurementPackage { PackageNumber = "PP-300", ProjectId = project.ProjectId, Title = "Valves", IsActive = true, RequiresInspection = true };
        ctx.ProcurementPackages.Add(package);
        await ctx.SaveChangesAsync();

        var itp = new InspectionTestPlan
        {
            ITPNumber = "ITP-300",
            ProjectId = project.ProjectId,
            ProcurementPackageId = package.ProcurementPackageId,
            QCDisciplineId = discipline.QCDisciplineId,
            Title = "Valve Inspection",
            IsActive = true
        };
        ctx.InspectionTestPlans.Add(itp);
        await ctx.SaveChangesAsync();

        var service = new RelationshipService(ctx);

        var result = await service.ExploreFromProcurementPackageAsync(package.ProcurementPackageId);

        Assert.NotNull(result);
        Assert.Contains(result!.InspectionTestPlans, n => n.NodeId == itp.InspectionTestPlanId);
    }
}
