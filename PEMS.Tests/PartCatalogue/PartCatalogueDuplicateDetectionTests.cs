using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Application.PartCatalogue;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.PartCatalogue;

/// <summary>
/// Procurement Lifecycle WP-C (Phase 1): جستجوی قطعه‌ی موجود و کاندیدهای تکراری MPN (بدون ادغام خودکار) - فقط‌خواندنی و بدون
/// نیاز به هیچ ستون/جدول تازه. پیوند Material Master و منشأ تقاضا (نیازمند تغییر Schema) تأیید نشده و در این فاز تحویل نشد.
/// </summary>
public class PartCatalogueDuplicateDetectionTests
{
    private const int Owner = 1, OtherEmployee = 2;

    private sealed class Env
    {
        public required PEMSDbContext Db { get; init; }
        public required PartCatalogueDuplicateService Dup { get; init; }
    }

    private static Env NewEnv()
    {
        var db = PemsDbContextFactory.CreateInMemory("pcd_" + Guid.NewGuid().ToString("N"));
        return new Env { Db = db, Dup = new PartCatalogueDuplicateService(db) };
    }

    private static async Task<PartCatalogueRequest> AddRequestAsync(Env e, string status, string? partNo = null, string? mpn = null,
        string? supplierPn = null, string? mfrName = null, string? oldMpn = null, string? description = null, int requester = Owner)
    {
        var r = new PartCatalogueRequest
        {
            RequesterEmployeeId = requester, Status = status, AssignedPartNumber = partNo, ManufacturerPartNumber = mpn,
            SupplierPartNumber = supplierPn, NewManufacturerName = mfrName, IsNewManufacturer = mfrName != null,
            OldManufacturerPartNumber = oldMpn, CompletedDate = status == "Completed" ? DateTime.Now : null
        };
        if (description != null)
            r.Attributes.Add(new PartCatalogueRequestAttribute { Name = PartCatalogueRequestAttribute.FreeFormatText, Value = description });
        e.Db.PartCatalogueRequests.Add(r);
        await e.Db.SaveChangesAsync();
        return r;
    }

    private static async Task<MaterialItem> AddMaterialAsync(Env e, string code, string? mpn = null, string? mfr = null, string desc = "item", bool active = true)
    {
        var m = new MaterialItem { MaterialCode = code, Description = desc, ManufacturerPartNumber = mpn, Manufacturer = mfr, IsActive = active };
        e.Db.MaterialItems.Add(m);
        await e.Db.SaveChangesAsync();
        return m;
    }

    // ───────────────────────── Rules ─────────────────────────

    [Theory]
    [InlineData("ab-123 / x", "AB123X")]
    [InlineData(" 6ES7 - 214.1AG40_0XB0 ", "6ES72141AG400XB0")]
    [InlineData(null, "")]
    public void NormalizePartNumber_StripsSeparators_AndUppercases(string? input, string expected)
        => Assert.Equal(expected, PartCatalogueRules.NormalizePartNumber(input));

    [Theory]
    [InlineData("NO_PART_NR", false)]   // مقدار دستورالعمل برای سازنده LOCAL
    [InlineData("n/a", false)]
    [InlineData("TBD", false)]
    [InlineData("12", false)]           // بسیار کوتاه
    [InlineData("LV-1", true)]          // داده‌ی واقعی تست: ۳ نویسه‌ی معنادار
    [InlineData("6ES7214-1AG40", true)]
    public void IsMatchablePartNumber_RejectsPlaceholdersAndTooShort(string value, bool expected)
        => Assert.Equal(expected, PartCatalogueRules.IsMatchablePartNumber(value));

    [Fact]
    public void CodesMatch_IgnoresCaseAndEdgeWhitespace_ButNotStructure()
    {
        Assert.True(PartCatalogueRules.CodesMatch(" pn-1000 ", "PN-1000"));
        Assert.False(PartCatalogueRules.CodesMatch("PN1000", "PN-1000"));
        Assert.False(PartCatalogueRules.CodesMatch(null, "PN-1000"));
    }

    // ───────────────────────── Duplicate candidates ─────────────────────────

    [Fact]
    public async Task Duplicates_FindSameMpnDespiteFormatting_AndFlagManufacturer()
    {
        var e = NewEnv();
        await AddRequestAsync(e, "Completed", partNo: "PN-1", mpn: "6ES7 214-1AG40", mfrName: "Siemens");

        var same = await e.Dup.FindDuplicateCandidatesAsync(new PcDuplicateProbe { ManufacturerPartNumber = "6es7214-1ag40", NewManufacturerName = "SIEMENS" });
        var other = await e.Dup.FindDuplicateCandidatesAsync(new PcDuplicateProbe { ManufacturerPartNumber = "6ES7214-1AG40", NewManufacturerName = "ABB" });
        var unknown = await e.Dup.FindDuplicateCandidatesAsync(new PcDuplicateProbe { ManufacturerPartNumber = "6ES7214-1AG40" });

        Assert.Equal("SameMpnSameManufacturer", Assert.Single(same.Candidates).MatchKind);
        Assert.Equal("SameMpnOtherManufacturer", Assert.Single(other.Candidates).MatchKind);
        Assert.Equal("SameMpnManufacturerUnknown", Assert.Single(unknown.Candidates).MatchKind);
        Assert.False(same.IsAmbiguous);
        Assert.False(same.AutoMergeAllowed);
    }

    [Fact]
    public async Task Duplicates_NeverMatchPlaceholderMpns()
    {
        var e = NewEnv();
        await AddRequestAsync(e, "Completed", partNo: "PN-1", mpn: "NO_PART_NR");
        await AddRequestAsync(e, "Completed", partNo: "PN-2", mpn: "NO_PART_NR");

        var r = await e.Dup.FindDuplicateCandidatesAsync(new PcDuplicateProbe { ManufacturerPartNumber = "NO_PART_NR" });

        Assert.Empty(r.Candidates);
    }

    [Fact]
    public async Task Duplicates_TwoDistinctStrongParts_AreAmbiguous_AndNothingIsMerged()
    {
        var e = NewEnv();
        await AddRequestAsync(e, "Completed", partNo: "PN-A", mpn: "XYZ-100", mfrName: "ACME");
        await AddRequestAsync(e, "Completed", partNo: "PN-B", mpn: "XYZ100", mfrName: "Acme");

        var r = await e.Dup.FindDuplicateCandidatesAsync(new PcDuplicateProbe { ManufacturerPartNumber = "XYZ 100", NewManufacturerName = "ACME" });

        Assert.Equal(2, r.Candidates.Count);
        Assert.True(r.IsAmbiguous);
        Assert.False(r.AutoMergeAllowed);
        Assert.Equal(2, await e.Db.PartCatalogueRequests.CountAsync());   // هیچ رکوردی تغییر/حذف نشد
    }

    [Fact]
    public async Task Duplicates_MatchOldMpn_MaterialMaster_AndDoNotDoubleCountALinkedPart()
    {
        var e = NewEnv();
        var mi = await AddMaterialAsync(e, "PN-LINKED", mpn: "LINK-777", mfr: "ACME");
        await AddRequestAsync(e, "Completed", partNo: "PN-LINKED", mpn: "LINK-777", mfrName: "ACME");
        await AddRequestAsync(e, "Completed", partNo: "PN-OLD", mpn: "NEWCODE-1", oldMpn: "OLDCODE-9");
        await AddMaterialAsync(e, "MM-ONLY", mpn: "MMONLY-5", mfr: "ACME");
        await e.Db.SaveChangesAsync();

        var linkedHit = await e.Dup.FindDuplicateCandidatesAsync(new PcDuplicateProbe { ManufacturerPartNumber = "LINK777", NewManufacturerName = "ACME" });
        var oldHit = await e.Dup.FindDuplicateCandidatesAsync(new PcDuplicateProbe { ManufacturerPartNumber = "OLDCODE9" });
        var mmHit = await e.Dup.FindDuplicateCandidatesAsync(new PcDuplicateProbe { ManufacturerPartNumber = "MMONLY5", NewManufacturerName = "Acme" });

        Assert.Single(linkedHit.Candidates);                 // درخواست + MaterialItem پیوندخورده = یک قطعه
        Assert.False(linkedHit.IsAmbiguous);
        Assert.Equal("OldManufacturerPartNumber", Assert.Single(oldHit.Candidates).MatchedOn);
        var mm = Assert.Single(mmHit.Candidates);
        Assert.Equal("MaterialItem", mm.Source);
        Assert.Equal("SameMpnSameManufacturer", mm.MatchKind);
    }

    [Fact]
    public async Task Duplicates_DoNotDisclosePendingRequests_OnlyCountThem_AndExcludeSelf()
    {
        var e = NewEnv();
        var mine = await AddRequestAsync(e, "Draft", mpn: "PEND-100");
        await AddRequestAsync(e, "InApproval", mpn: "PEND-100", requester: OtherEmployee);
        await AddRequestAsync(e, "Cancelled", mpn: "PEND-100", requester: OtherEmployee);

        var r = await e.Dup.FindDuplicateCandidatesAsync(new PcDuplicateProbe { ManufacturerPartNumber = "PEND100", ExcludeRequestId = mine.PartCatalogueRequestId });

        Assert.Empty(r.Candidates);
        Assert.Equal(1, r.InFlightRequestsWithSameMpn);
    }

    // ───────────────────────── Existing-part search ─────────────────────────

    [Fact]
    public async Task Search_RequiresAtLeastTwoCharacters_AndOnlyReturnsApprovedParts()
    {
        var e = NewEnv();
        await AddRequestAsync(e, "Completed", partNo: "VALVE-001", mpn: "V-100");
        await AddRequestAsync(e, "Draft", partNo: null, mpn: "V-100");
        await AddMaterialAsync(e, "MM-VALVE-9", desc: "Gate valve 2in");

        Assert.Empty(await e.Dup.SearchExistingPartsAsync("v"));
        Assert.Empty(await e.Dup.SearchExistingPartsAsync("   "));

        var byCode = await e.Dup.SearchExistingPartsAsync("valve 001");
        Assert.Equal("VALVE-001", Assert.Single(byCode).PartNumber);
        Assert.Equal("Exact", byCode[0].MatchKind);

        var byDesc = await e.Dup.SearchExistingPartsAsync("gate valve");
        Assert.Equal("MM-VALVE-9", Assert.Single(byDesc).PartNumber);
    }

    [Fact]
    public async Task Search_MatchesFreeFormatText_AndPrefersExactOverPartial()
    {
        var e = NewEnv();
        await AddRequestAsync(e, "Completed", partNo: "ZZ-1", description: "BALL VALVE CARBON STEEL");
        await AddRequestAsync(e, "Completed", partNo: "AB-12", mpn: "AB12");
        await AddRequestAsync(e, "Completed", partNo: "AB-123", mpn: "AB123X");

        var text = await e.Dup.SearchExistingPartsAsync("carbon steel");
        Assert.Equal("ZZ-1", Assert.Single(text).PartNumber);

        var ids = await e.Dup.SearchExistingPartsAsync("ab12");
        Assert.Equal("AB-12", ids[0].PartNumber);          // Exact اول
        Assert.Equal("Exact", ids[0].MatchKind);
        Assert.Contains(ids, h => h.PartNumber == "AB-123" && h.MatchKind == "Partial");
    }
}
