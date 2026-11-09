using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Procurement;

/// <summary>
/// SCRUM-544, 546, 547 (Sprint 1 review bugs): every procurement decision has a page its role can open and act on.
/// - 544: Vendor details has the qualification form (Vendor Edit) and history, posting to the existing SetQualification action.
/// - 546: the PO page offers "Route again" for an invalid route; the authority page offers only roles with PurchaseOrder Approve.
/// - 547: the IRN QA buttons only for IRN Approve.
/// </summary>
public class ProcurementDecisionPagesTests
{
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { SupportedReleases.RepoRoot() }.Concat(parts).ToArray()));

    // ------------------------------------------------------------ SCRUM-544

    [Fact]
    public void SCRUM544_Vendor_details_posts_the_qualification_form_to_SetQualification_with_the_action_parameter_names()
    {
        var view = Read("PEMS.Web", "Views", "Vendor", "Details.cshtml");

        var form = view[view.IndexOf("@if (canEditVendor)", StringComparison.Ordinal)..];
        Assert.Contains("Url.Action(\"SetQualification\")", form);
        Assert.Contains("parent-name=\"id\"", form);
        foreach (var name in new[] { "newStatus", "category", "effectiveDate", "expiryDate", "evidenceReference", "reason" })
            Assert.Contains($"name=\"{name}\"", form);
        Assert.Contains("q.History", view);   // history for every viewer

        var controller = Read("PEMS.Web", "Controllers", "VendorController.cs");
        Assert.Contains("ViewBag.Qualification = await _qualificationService.GetAsync(id)", controller);
        Assert.Contains("ViewBag.CanEditVendor = await _permissionService.HasPermissionAsync(_currentUser.Roles, \"Vendors\", \"Edit\")", controller);
    }

    private static PEMSDbContext NewDb() => new(new DbContextOptionsBuilder<PEMSDbContext>()
        .UseInMemoryDatabase("vq_" + Guid.NewGuid().ToString("N")).Options);

    [Fact]
    public async Task SCRUM544_Qualifying_a_vendor_records_the_history_and_the_view_shows_it_newest_first()
    {
        await using var db = NewDb();
        var vendor = new Vendor { Name = "KMC", IsActive = true };
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync();
        var service = new VendorQualificationService(db);

        Assert.True((await service.SetQualificationAsync(vendor.VendorId, "Pending", " Mechanical ", null, null, null, "documents received", null)).Success);
        Assert.True((await service.SetQualificationAsync(vendor.VendorId, "Qualified", "Mechanical", new DateTime(2026, 11, 1), new DateTime(2027, 10, 31), "QA-REP-17", "audit passed", null)).Success);

        var view = (await service.GetAsync(vendor.VendorId))!;
        Assert.Equal(("Qualified", "Mechanical", "QA-REP-17"), (view.Status, view.Category, view.EvidenceReference));
        Assert.Equal(new[] { "Qualified", "Pending" }, view.History.Select(h => h.NewStatus));
        Assert.Equal(new[] { "Pending", "NotQualified" }, view.History.Select(h => h.PreviousStatus));
        Assert.Equal("Mechanical", view.History.Last().Category);   // trimmed
        Assert.Null(await service.GetAsync(vendor.VendorId + 1));
    }

    [Fact]
    public async Task SCRUM544_Qualification_errors_are_resource_keys_and_change_nothing()
    {
        await using var db = NewDb();
        var vendor = new Vendor { Name = "KMC", IsActive = true };
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync();
        var service = new VendorQualificationService(db);

        Assert.Equal("Vq_Err_Status", (await service.SetQualificationAsync(vendor.VendorId, "Gold", null, null, null, null, "x", null)).Error);
        Assert.Equal("Vq_Err_Dates", (await service.SetQualificationAsync(vendor.VendorId, "Qualified", null, new DateTime(2026, 11, 2), new DateTime(2026, 11, 1), null, "x", null)).Error);
        Assert.Equal("Vq_Err_NotFound", (await service.SetQualificationAsync(vendor.VendorId + 1, "Qualified", null, null, null, null, "x", null)).Error);

        Assert.Equal("NotQualified", (await service.GetAsync(vendor.VendorId))!.Status);
        Assert.Empty(db.VendorQualificationHistories);
        Assert.Equal(new[] { "NotQualified", "Pending", "Qualified", "Suspended", "Expired" }, IVendorQualificationService.Statuses);
    }

    // ------------------------------------------------------------ SCRUM-546

    [Fact]
    public void SCRUM546_The_PO_page_offers_route_again_for_an_invalid_route_and_the_authority_page_only_approver_roles()
    {
        var view = Read("PEMS.Web", "Views", "PurchaseOrder", "Details.cshtml");
        var block = view[view.IndexOf("@if (a.RouteInvalid)", StringComparison.Ordinal)..];
        Assert.Contains("Localizer[\"Po_RouteInvalid\"]", block);
        Assert.Contains("asp-action=\"RouteAgain\"", block[..block.IndexOf("@if (isDraft", StringComparison.Ordinal)]);

        var controller = Read("PEMS.Web", "Areas", "Admin", "Controllers", "PurchaseOrderAuthorityController.cs");
        Assert.Contains("ViewBag.Roles = await _service.GetApproverRolesAsync()", controller);
    }

    // ------------------------------------------------------------ SCRUM-547

    [Fact]
    public void SCRUM547_The_IRN_QA_buttons_show_only_with_IRN_Approve()
    {
        var view = Read("PEMS.Web", "Areas", "Warehouse", "Views", "IrnReceipt", "Details.cshtml");
        var gate = view.IndexOf("@if (Model.Status == \"PendingApproval\" && canApproveQa)", StringComparison.Ordinal);
        Assert.True(gate > 0);
        Assert.Equal(gate, view.IndexOf("asp-action=\"Approve\"", StringComparison.Ordinal) is var first and > 0
            ? view.LastIndexOf("@if (", first, StringComparison.Ordinal) : -1);

        var controller = Read("PEMS.Web", "Areas", "Warehouse", "Controllers", "IrnReceiptController.cs");
        Assert.Contains("ViewBag.CanApproveQa = await _permissionService.HasPermissionAsync(_currentUser.Roles, \"IRN\", \"Approve\")", controller);
    }
}
