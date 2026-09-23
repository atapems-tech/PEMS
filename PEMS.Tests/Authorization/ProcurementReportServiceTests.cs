using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق گزارش سخت‌افزاری نهایی Package Procurement (Gap ۵): صحت داده‌ی زنجیره‌ی
/// Tender→Bid→Contract→PO→NTP در گزارش تدارکات - پیش از این، این داده‌ها اصلاً در گزارش
/// دیده نمی‌شدند.
/// </summary>
public class ProcurementReportServiceTests
{
    private static async Task<(PEMS.Persistence.Context.PEMSDbContext Ctx, ProcurementReportService Service)> CreateAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"ProcReport_{dbName}");

        var project = new Project { ProjectCode = "P1", ProjectName = "Project One", IsActive = true };
        ctx.Projects.Add(project);
        var vendor = new Vendor { Name = "Vendor A", IsActive = true };
        ctx.Vendors.Add(vendor);
        await ctx.SaveChangesAsync();

        var package = new ProcurementPackage { PackageNumber = "PP-001", ProjectId = project.ProjectId, Title = "T", IsActive = true };
        ctx.ProcurementPackages.Add(package);
        await ctx.SaveChangesAsync();

        var contract = new Contract { ContractNo = "CT-001", ProjectId = project.ProjectId, VendorId = vendor.VendorId, ContractValue = 100000m, Status = "Signed", IsActive = true };
        ctx.Contracts.Add(contract);
        await ctx.SaveChangesAsync();

        var tender = new Tender { TenderNumber = "TND-0001", ProcurementPackageId = package.ProcurementPackageId, Title = "T", Status = "Awarded", ContractId = contract.ContractId };
        ctx.Tenders.Add(tender);
        await ctx.SaveChangesAsync();

        var bid = new Bid { BidNumber = "BID-0001", TenderId = tender.TenderId, VendorId = vendor.VendorId, OriginalValue = 100000m, Status = "Won" };
        ctx.Bids.Add(bid);
        await ctx.SaveChangesAsync();

        tender.AwardedBidId = bid.BidId;
        ctx.Bids.Add(new Bid { BidNumber = "BID-0002", TenderId = tender.TenderId, VendorId = vendor.VendorId, OriginalValue = 90000m, Status = "Lost" });

        ctx.PurchaseOrders.Add(new PurchaseOrder { PoNumber = "PO-001", ProjectId = project.ProjectId, VendorId = vendor.VendorId, ContractId = contract.ContractId, Status = "Issued" });
        ctx.ContractNTPs.Add(new ContractNTP { ContractId = contract.ContractId, NTPNumber = "NTP-0001", Status = "Issued" });

        await ctx.SaveChangesAsync();

        return (ctx, new ProcurementReportService(ctx));
    }

    [Fact]
    public async Task GetReportAsync_IncludesTenderBidContractPoNtpCounts()
    {
        var (_, service) = await CreateAsync(nameof(GetReportAsync_IncludesTenderBidContractPoNtpCounts));

        var report = await service.GetReportAsync();

        Assert.Equal(1, report.TotalTenders);
        Assert.Equal(2, report.TotalBids);
        Assert.Equal(1, report.TotalContracts);
        Assert.Equal(1, report.TotalPurchaseOrders);
        Assert.Equal(1, report.TotalNTPsIssued);
        Assert.Contains(report.ByContractStatus, x => x.Status == "Signed" && x.Count == 1);
    }

    [Fact]
    public async Task GetPipelineAsync_ReturnsFullLifecycleRow()
    {
        var (_, service) = await CreateAsync(nameof(GetPipelineAsync_ReturnsFullLifecycleRow));

        var pipeline = await service.GetPipelineAsync();

        Assert.Equal(1, pipeline.TotalCount);
        var row = pipeline.Items.Single();
        Assert.Equal("PP-001", row.PackageNumber);
        Assert.Equal("TND-0001", row.TenderNumber);
        Assert.Equal("Vendor A", row.WinnerVendorName);
        Assert.Equal("CT-001", row.ContractNo);
        Assert.Equal("Signed", row.ContractStatus);
        Assert.Equal(100000m, row.ContractValue);
        Assert.Equal("PO-001", row.PoNumber);
        Assert.Equal("Issued", row.NtpStatus);
    }

    [Fact]
    public async Task GetPipelineAsync_RespectsPageSize()
    {
        var (ctx, service) = await CreateAsync(nameof(GetPipelineAsync_RespectsPageSize));
        var package = ctx.ProcurementPackages.Single();
        for (var i = 0; i < 5; i++)
            ctx.Tenders.Add(new Tender { TenderNumber = $"TND-{i:0000}extra", ProcurementPackageId = package.ProcurementPackageId, Title = "T", Status = "Draft" });
        await ctx.SaveChangesAsync();

        var pipeline = await service.GetPipelineAsync(page: 1, pageSize: 3);

        Assert.Equal(6, pipeline.TotalCount);
        Assert.Equal(3, pipeline.Items.Count);
    }
}
