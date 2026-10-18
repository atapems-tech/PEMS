using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق ممیزی Package Procurement Contract Finalization (§C/§D/§I، Phase 3.9): پوشش قواعد
/// حیاتی - بی‌بازگشت‌بودن پیشنهاد اصلی، گیت مستقل فنی/تجاری، گیت صلاحیت فروشنده برای بسته‌های
/// بحرانی، و زنجیره‌ی کامل تا ساخت Contract Draft.
/// </summary>
public class TenderServiceTests
{
    private static async Task<(PEMSDbContext Ctx, TenderService Service, int PackageId, int QualifiedVendorId, int UnqualifiedVendorId)> CreateAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"TenderSvc_{dbName}");

        var project = new Project { ProjectCode = "P1", ProjectName = "Project One", IsActive = true };
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        var package = new ProcurementPackage { PackageNumber = "PP-001", ProjectId = project.ProjectId, Title = "Pumps Package", IsActive = true };
        ctx.ProcurementPackages.Add(package);

        var qualifiedVendor = new Vendor { Name = "Qualified Vendor", IsActive = true, QualificationStatus = "Qualified", QualificationExpiryDate = DateTime.Now.AddYears(1) };
        var unqualifiedVendor = new Vendor { Name = "Unqualified Vendor", IsActive = true, QualificationStatus = "Pending" };
        ctx.Vendors.Add(qualifiedVendor);
        ctx.Vendors.Add(unqualifiedVendor);
        await ctx.SaveChangesAsync();

        var numbering = new FakeNumberingService();
        var service = new TenderService(ctx, numbering);

        return (ctx, service, package.ProcurementPackageId, qualifiedVendor.VendorId, unqualifiedVendor.VendorId);
    }

    [Fact]
    public async Task InviteVendor_UnqualifiedVendor_OnCriticalTender_IsRejected()
    {
        var (ctx, service, packageId, _, unqualifiedVendorId) = await CreateAsync(nameof(InviteVendor_UnqualifiedVendor_OnCriticalTender_IsRejected));
        var tenderId = await service.CreateAsync(packageId, "Pump Tender", null, null, requiresVendorQualification: true, null, null, null, null);

        var result = await service.InviteVendorAsync(tenderId, unqualifiedVendorId);

        Assert.False(result.Success);
        Assert.Empty(ctx.TenderInvitedVendors);
    }

    [Fact]
    public async Task InviteVendor_QualifiedVendor_OnCriticalTender_Succeeds_AndSnapshotsConfirmation()
    {
        var (ctx, service, packageId, qualifiedVendorId, _) = await CreateAsync(nameof(InviteVendor_QualifiedVendor_OnCriticalTender_Succeeds_AndSnapshotsConfirmation));
        var tenderId = await service.CreateAsync(packageId, "Pump Tender", null, null, requiresVendorQualification: true, null, null, null, null);

        var result = await service.InviteVendorAsync(tenderId, qualifiedVendorId);

        Assert.True(result.Success);
        Assert.True(ctx.TenderInvitedVendors.Single().QualificationConfirmed);
    }

    [Fact]
    public async Task SubmitBid_ByNonInvitedVendor_IsRejected()
    {
        var (ctx, service, packageId, qualifiedVendorId, _) = await CreateAsync(nameof(SubmitBid_ByNonInvitedVendor_IsRejected));
        var tenderId = await service.CreateAsync(packageId, "Pump Tender", null, null, false, null, null, null, null);
        await service.InviteVendorAsync(tenderId, qualifiedVendorId);
        await service.OpenForBiddingAsync(tenderId);

        var otherVendor = new Vendor { Name = "Not Invited", IsActive = true };
        ctx.Vendors.Add(otherVendor);
        await ctx.SaveChangesAsync();

        var result = await service.SubmitBidAsync(tenderId, otherVendor.VendorId, 1000m, "USD", null, null);

        Assert.False(result.Success);
        Assert.Empty(ctx.Bids);
    }

    [Fact]
    public async Task SubmitBid_ThenNegotiate_OriginalValueIsNeverOverwritten()
    {
        var (ctx, service, packageId, qualifiedVendorId, _) = await CreateAsync(nameof(SubmitBid_ThenNegotiate_OriginalValueIsNeverOverwritten));
        var tenderId = await service.CreateAsync(packageId, "Pump Tender", null, null, false, null, null, null, null);
        await service.InviteVendorAsync(tenderId, qualifiedVendorId);
        await service.OpenForBiddingAsync(tenderId);

        var bidResult = await service.SubmitBidAsync(tenderId, qualifiedVendorId, 100000m, "USD", null, null);
        Assert.True(bidResult.Success);

        var negotiationResult = await service.RequestNegotiationAsync(
            bidResult.BidId!.Value, 92000m, "Buyer, Vendor", "Discount applied", null, null, "Volume discount", null, 1);
        Assert.True(negotiationResult.Success);

        await service.ApproveNegotiationAsync(negotiationResult.NegotiationId!.Value, 2);

        var bid = ctx.Bids.Single();
        Assert.Equal(100000m, bid.OriginalValue); // اصلی هرگز بازنویسی نمی‌شود
        Assert.Equal(92000m, bid.NegotiatedValue);
    }

    [Fact]
    public async Task SelectWinner_WithOnlyTechnicalPassed_IsRejected()
    {
        var (ctx, service, packageId, qualifiedVendorId, _) = await CreateAsync(nameof(SelectWinner_WithOnlyTechnicalPassed_IsRejected));
        var tenderId = await service.CreateAsync(packageId, "Pump Tender", null, null, false, null, null, null, null);
        await service.InviteVendorAsync(tenderId, qualifiedVendorId);
        await service.OpenForBiddingAsync(tenderId);
        var bidResult = await service.SubmitBidAsync(tenderId, qualifiedVendorId, 100000m, "USD", null, null);
        await service.CloseBiddingAsync(tenderId);

        await service.RecordBidEvaluationAsync(bidResult.BidId!.Value, "Technical", 1, "Engineering", "Passed", null);
        // تجاری عمداً ثبت نشده

        var result = await service.SelectWinnerAsync(tenderId, bidResult.BidId!.Value, 1);

        Assert.False(result.Success);
        Assert.Equal("Closed", ctx.Tenders.Single().Status);
    }

    [Fact]
    public async Task SelectWinner_WithOnlyCommercialPassed_IsRejected()
    {
        var (ctx, service, packageId, qualifiedVendorId, _) = await CreateAsync(nameof(SelectWinner_WithOnlyCommercialPassed_IsRejected));
        var tenderId = await service.CreateAsync(packageId, "Pump Tender", null, null, false, null, null, null, null);
        await service.InviteVendorAsync(tenderId, qualifiedVendorId);
        await service.OpenForBiddingAsync(tenderId);
        var bidResult = await service.SubmitBidAsync(tenderId, qualifiedVendorId, 100000m, "USD", null, null);
        await service.CloseBiddingAsync(tenderId);

        // فنی عمداً ثبت نشده - فقط تجاری
        await service.RecordBidEvaluationAsync(bidResult.BidId!.Value, "Commercial", 1, "Finance", "Passed", null);

        var result = await service.SelectWinnerAsync(tenderId, bidResult.BidId!.Value, 1);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task SelectWinner_WithBothEvaluationsPassed_Succeeds_AndClosesOtherBidsAsLost()
    {
        var (ctx, service, packageId, qualifiedVendorId, _) = await CreateAsync(nameof(SelectWinner_WithBothEvaluationsPassed_Succeeds_AndClosesOtherBidsAsLost));
        var tenderId = await service.CreateAsync(packageId, "Pump Tender", null, null, false, null, null, null, null);
        await service.InviteVendorAsync(tenderId, qualifiedVendorId);

        var secondVendor = new Vendor { Name = "Second Vendor", IsActive = true };
        ctx.Vendors.Add(secondVendor);
        await ctx.SaveChangesAsync();
        await service.InviteVendorAsync(tenderId, secondVendor.VendorId);

        await service.OpenForBiddingAsync(tenderId);
        var winningBid = await service.SubmitBidAsync(tenderId, qualifiedVendorId, 100000m, "USD", null, null);
        var losingBid = await service.SubmitBidAsync(tenderId, secondVendor.VendorId, 110000m, "USD", null, null);
        await service.CloseBiddingAsync(tenderId);

        await service.RecordBidEvaluationAsync(winningBid.BidId!.Value, "Technical", 1, "Engineering", "Passed", null);
        await service.RecordBidEvaluationAsync(winningBid.BidId!.Value, "Commercial", 1, "Finance", "Passed", null);

        var result = await service.SelectWinnerAsync(tenderId, winningBid.BidId!.Value, 1);

        Assert.True(result.Success);
        Assert.Equal("Awarded", ctx.Tenders.Single().Status);
        Assert.Equal("Won", ctx.Bids.Single(x => x.BidId == winningBid.BidId).Status);
        Assert.Equal("Lost", ctx.Bids.Single(x => x.BidId == losingBid.BidId).Status);
    }

    [Fact]
    public async Task CreateContractFromWinningBid_BeforeAwarded_Fails()
    {
        var (_, service, packageId, qualifiedVendorId, _) = await CreateAsync(nameof(CreateContractFromWinningBid_BeforeAwarded_Fails));
        var tenderId = await service.CreateAsync(packageId, "Pump Tender", null, null, false, null, null, null, null);

        var result = await service.CreateContractFromWinningBidAsync(tenderId, "CT-001", null, null);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateContractFromWinningBid_AfterAwarded_CreatesDraftContract_WithBidFinalValue()
    {
        var (ctx, service, packageId, qualifiedVendorId, _) = await CreateAsync(nameof(CreateContractFromWinningBid_AfterAwarded_CreatesDraftContract_WithBidFinalValue));
        var tenderId = await service.CreateAsync(packageId, "Pump Tender", null, null, false, null, null, null, null);
        await service.InviteVendorAsync(tenderId, qualifiedVendorId);
        await service.OpenForBiddingAsync(tenderId);
        var bidResult = await service.SubmitBidAsync(tenderId, qualifiedVendorId, 100000m, "USD", null, null);
        await service.CloseBiddingAsync(tenderId);
        await service.RecordBidEvaluationAsync(bidResult.BidId!.Value, "Technical", 1, "Engineering", "Passed", null);
        await service.RecordBidEvaluationAsync(bidResult.BidId!.Value, "Commercial", 1, "Finance", "Passed", null);
        await service.SelectWinnerAsync(tenderId, bidResult.BidId!.Value, 1);

        var result = await service.CreateContractFromWinningBidAsync(tenderId, "CT-001", DateTime.Today, DateTime.Today.AddYears(1));

        Assert.True(result.Success);
        var contract = ctx.Contracts.Single();
        Assert.Equal("CT-001", contract.ContractNo);
        Assert.Equal(qualifiedVendorId, contract.VendorId);
        Assert.Equal(100000m, contract.ContractValue);
        Assert.Equal("Draft", contract.Status);
        Assert.Equal(contract.ContractId, ctx.Tenders.Single().ContractId);
    }
}
