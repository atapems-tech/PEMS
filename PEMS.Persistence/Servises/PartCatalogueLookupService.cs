using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Application.PartCatalogue;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// Lookupهای فرم درخواست کاتالوگ قطعه. فهرست‌های بزرگ (Technical Class ~۴۱۶۱، Manufacturer، Supplier، Equipment) فقط با
/// جستجوی سمت سرور و سقف نتیجه برمی‌گردند (بدون بارگذاری کل جدول در مرورگر)؛ همه‌ی کوئری‌ها روی ستون‌های ایندکس‌شده/محدودند.
/// </summary>
public class PartCatalogueLookupService : IPartCatalogueLookupService
{
    private const int MaxTake = 50;
    private readonly PEMSDbContext _ctx;

    public PartCatalogueLookupService(PEMSDbContext ctx) => _ctx = ctx;

    private static int Take(int take) => Math.Clamp(take, 1, MaxTake);

    public async Task<List<PcLookupItem>> SearchTechnicalClassesAsync(string? q, int take = 30)
    {
        q = q?.Trim();
        var query = _ctx.PartCatalogueTechnicalClasses.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrEmpty(q))
            query = query.Where(x => x.Code.Contains(q) || x.Description.Contains(q));

        var rows = await query
            .OrderBy(x => q != null && x.Code.StartsWith(q) ? 0 : q != null && x.Description.StartsWith(q) ? 1 : 2)
            .ThenBy(x => x.Description).ThenBy(x => x.Code)
            .Take(Take(take))
            .Select(x => new { x.TechnicalClassId, x.Code, x.Description })
            .ToListAsync();

        return rows.Select(x => new PcLookupItem { Id = x.TechnicalClassId, Code = x.Code, Text = $"{x.Code} - {x.Description}" }).ToList();
    }

    public async Task<PcLookupItem?> GetTechnicalClassAsync(int id)
    {
        var x = await _ctx.PartCatalogueTechnicalClasses.AsNoTracking().Where(t => t.TechnicalClassId == id)
            .Select(t => new { t.TechnicalClassId, t.Code, t.Description, t.LongDescription }).FirstOrDefaultAsync();
        return x == null ? null : new PcLookupItem { Id = x.TechnicalClassId, Code = x.Code, Text = $"{x.Code} - {x.Description}", Extra = x.LongDescription };
    }

    public async Task<PcTechnicalClassResolution> ResolveTechnicalClassAsync(string? freeFormatText)
    {
        var classes = await _ctx.PartCatalogueTechnicalClasses.AsNoTracking().Where(x => x.IsActive)
            .Select(x => new TechnicalClassRef(x.TechnicalClassId, x.Code, x.Description)).ToListAsync();
        var r = new TechnicalClassMatcher(classes).Resolve(freeFormatText);

        static PcLookupItem Item(TechnicalClassRef c) => new() { Id = c.Id, Code = c.Code, Text = $"{c.Code} - {c.Description}" };
        var dto = new PcTechnicalClassResolution
        {
            Status = r.Status switch { TechnicalClassResolutionStatus.Resolved => "resolved", TechnicalClassResolutionStatus.Ambiguous => "ambiguous", _ => "none" },
            Reason = r.Reason,
            Candidates = r.Candidates.Select(Item).ToList()
        };
        if (r.Resolved != null) dto.Resolved = await GetTechnicalClassAsync(r.Resolved.Id);   // شامل LongDescription (Extra)
        return dto;
    }

    public async Task<List<PcLookupItem>> SearchManufacturersAsync(string? q, int take = 30)
    {
        q = q?.Trim();
        // سازنده‌های «(wrong)» هرگز در لیست انتخاب نمی‌آیند
        var query = _ctx.PartCatalogueManufacturers.AsNoTracking().Where(x => x.IsActive && !x.IsWrong);
        if (!string.IsNullOrEmpty(q)) query = query.Where(x => x.Name.Contains(q));

        var rows = await query
            .OrderBy(x => x.IsLocal ? 0 : 1)
            .ThenBy(x => q != null && x.Name.StartsWith(q) ? 0 : 1).ThenBy(x => x.Name)
            .Take(Take(take)).Select(x => new { x.ManufacturerId, x.Name, x.IsLocal }).ToListAsync();

        return rows.Select(x => new PcLookupItem { Id = x.ManufacturerId, Code = x.Name, Text = x.Name, Extra = x.IsLocal ? "LOCAL" : null }).ToList();
    }

    public async Task<List<PcLookupItem>> SearchSuppliersAsync(string? q, int take = 30)
    {
        q = q?.Trim();
        var query = _ctx.PartCatalogueSuppliers.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrEmpty(q)) query = query.Where(x => x.Name.Contains(q));

        var rows = await query.OrderBy(x => q != null && x.Name.StartsWith(q) ? 0 : 1).ThenBy(x => x.Name)
            .Take(Take(take)).Select(x => new { x.SupplierId, x.Name }).ToListAsync();
        return rows.Select(x => new PcLookupItem { Id = x.SupplierId, Code = x.Name, Text = x.Name }).ToList();
    }

    public async Task<List<PcLookupItem>> SearchCommodityGroupsAsync(string? q, int take = 30)
    {
        q = q?.Trim();
        var query = _ctx.PartCatalogueCommodityGroups.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrEmpty(q)) query = query.Where(x => x.Description.Contains(q) || x.Code.Contains(q));
        var rows = await query.OrderBy(x => x.Description).Take(Take(take)).Select(x => new { x.CommodityGroupId, x.Code, x.Description }).ToListAsync();
        return rows.Select(x => new PcLookupItem { Id = x.CommodityGroupId, Code = x.Code, Text = GroupText(x.Description, x.Code) }).ToList();
    }

    public async Task<List<PcLookupItem>> SearchEquipmentAsync(string? q, int take = 20)
    {
        q = q?.Trim();
        var query = _ctx.Equipments.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrEmpty(q)) query = query.Where(x => x.TagNumber.Contains(q) || x.Name.Contains(q));

        var rows = await query.OrderBy(x => q != null && x.TagNumber.StartsWith(q) ? 0 : 1).ThenBy(x => x.TagNumber)
            .Take(Take(take)).Select(x => new { x.EquipmentId, x.TagNumber, x.Name }).ToListAsync();
        return rows.Select(x => new PcLookupItem { Id = x.EquipmentId, Code = x.TagNumber, Text = $"{x.TagNumber} - {x.Name}" }).ToList();
    }

    public async Task<List<PcLookupItem>> GetSectionsAsync() =>
        (await _ctx.PartCatalogueSections.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .Select(x => new { x.SectionId, x.Code, x.NameFa }).ToListAsync())
        .Select(x => new PcLookupItem { Id = x.SectionId, Code = x.Code, Text = x.Code, Extra = x.NameFa }).ToList();

    private static string GroupText(string description, string code) => $"{description}-{code}";

    public async Task<List<PcLookupItem>> GetCommodityGroup1Async(int? sectionId)
    {
        var all = _ctx.PartCatalogueCommodityGroups.AsNoTracking().Where(x => x.IsActive);

        if (sectionId.HasValue)
        {
            var allowed = await _ctx.PartCatalogueSectionCommodityGroups.Where(m => m.SectionId == sectionId).Select(m => m.CommodityGroupId).ToListAsync();
            if (allowed.Count > 0) all = all.Where(x => allowed.Contains(x.CommodityGroupId));   // بدون ردیف = همه (Fallback)
        }

        return (await all.OrderBy(x => x.Description).Select(x => new { x.CommodityGroupId, x.Code, x.Description }).ToListAsync())
            .Select(x => new PcLookupItem { Id = x.CommodityGroupId, Code = x.Code, Text = GroupText(x.Description, x.Code) }).ToList();
    }

    public async Task<List<PcLookupItem>> GetCommodityGroup2Async(int? group1Id)
    {
        var all = _ctx.PartCatalogueCommodityGroups.AsNoTracking().Where(x => x.IsActive);

        if (group1Id.HasValue)
        {
            var allowed = await _ctx.PartCatalogueCommodityGroupChildren.Where(m => m.ParentGroupId == group1Id).Select(m => m.ChildGroupId).ToListAsync();
            if (allowed.Count > 0) all = all.Where(x => allowed.Contains(x.CommodityGroupId));
        }

        return (await all.OrderBy(x => x.Description).Select(x => new { x.CommodityGroupId, x.Code, x.Description }).ToListAsync())
            .Select(x => new PcLookupItem { Id = x.CommodityGroupId, Code = x.Code, Text = GroupText(x.Description, x.Code) }).ToList();
    }

    public async Task<List<PcLookupItem>> GetProductFamiliesAsync() =>
        (await _ctx.PartCatalogueProductFamilies.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Description)
            .Select(x => new { x.ProductFamilyId, x.Code, x.Description }).ToListAsync())
        .Select(x => new PcLookupItem { Id = x.ProductFamilyId, Code = x.Code, Text = GroupText(x.Description, x.Code) }).ToList();

    public async Task<List<PcLookupItem>> GetUnitsAsync() =>
        (await _ctx.MaterialUOMs.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code)
            .Select(x => new { x.MaterialUOMId, x.Code, x.Name }).ToListAsync())
        .Select(x => new PcLookupItem { Id = x.MaterialUOMId, Code = x.Code, Text = x.Code, Extra = x.Name }).ToList();

    public async Task<List<PcLookupItem>> GetCurrenciesAsync() =>
        (await _ctx.PartCatalogueCurrencies.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder)
            .Select(x => new { x.CurrencyId, x.Code, x.Name, x.NameFa }).ToListAsync())
        .Select(x => new PcLookupItem { Id = x.CurrencyId, Code = x.Code, Text = x.Code, Extra = x.NameFa ?? x.Name }).ToList();

    public async Task<List<PcLookupItem>> GetStockGradesAsync() =>
        (await _ctx.PartCatalogueStockGrades.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Grade)
            .Select(x => new { x.StockGradeId, x.Grade, x.Description, x.DescriptionFa }).ToListAsync())
        .Select(x => new PcLookupItem { Id = x.StockGradeId, Code = x.Grade.ToString(), Text = $"{x.Grade} - {x.Description}", Extra = x.DescriptionFa }).ToList();

    public async Task<List<PcLookupItem>> GetPlanningMethodsAsync() =>
        (await _ctx.PartCataloguePlanningMethods.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code)
            .Select(x => new { x.PlanningMethodId, x.Code, x.Description }).ToListAsync())
        .Select(x => new PcLookupItem { Id = x.PlanningMethodId, Code = x.Code, Text = x.Code, Extra = x.Description }).ToList();

    public async Task<List<PcLookupItem>> GetTypeDesignationsAsync() =>
        (await _ctx.PartCatalogueTypeDesignations.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder)
            .Select(x => new { x.TypeDesignationId, x.Code, x.IsCritical, x.IsCapital }).ToListAsync())
        .Select(x => new PcLookupItem
        {
            Id = x.TypeDesignationId, Code = x.Code, Text = x.Code,
            Extra = string.Join(",", new[] { x.IsCritical ? "CR" : null, x.IsCapital ? "CAP" : null }.Where(s => s != null))
        }).ToList();

    public async Task<(List<string> Exact, List<string> Similar)> CheckManufacturerNameAsync(string name)
    {
        name = PartCatalogueRules.NormalizeNewManufacturerName(name);
        if (name.Length < 2) return (new List<string>(), new List<string>());

        var key = PartCatalogueRules.NormalizeName(name);
        var first = key.Length >= 3 ? key[..3] : key;

        // فقط کاندیدهای هم‌پیشوند/هم‌شمول را بیاور (نه کل جدول)؛ مقایسه‌ی دقیق‌تر در Rules انجام می‌شود
        var candidates = await _ctx.PartCatalogueManufacturers.AsNoTracking()
            .Where(x => x.IsActive && !x.IsWrong && (x.NormalizedName.StartsWith(first) || x.NormalizedName.Contains(key) || key.Contains(x.NormalizedName)))
            .Select(x => new { x.Name, x.NormalizedName }).Take(500).ToListAsync();

        return PartCatalogueRules.FindManufacturerMatches(name, candidates.Select(c => (c.Name, c.NormalizedName)));
    }
}
