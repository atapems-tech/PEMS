using System.Diagnostics;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Application.PartCatalogue;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// Import فایل‌های مرجع IFS (Technical Class / Commodity Group / Product Family / فهرست Manufacturer و Supplier).
/// Upsert بر اساس «کد» (Idempotent). <c>updateExisting=false</c> (حالت Seed) فقط ردیف‌های جدید را اضافه می‌کند و هرگز
/// ویرایش/Import قبلی مدیر را برنمی‌گرداند؛ Import دستی مدیر <c>updateExisting=true</c> دارد.
/// </summary>
public class PartCatalogueAdminService : IPartCatalogueAdminService
{
    public const int MaxRows = 100_000;

    private readonly PEMSDbContext _ctx;
    private readonly IRoleModuleActionService? _actions;

    /// <param name="actions">null فقط برای Seed داخلی (بدون Actor)؛ در DI همیشه مقدار دارد.</param>
    public PartCatalogueAdminService(PEMSDbContext ctx, IRoleModuleActionService? actions = null)
    {
        _ctx = ctx;
        _actions = actions;
    }

    private async Task<bool> IsAdminAsync(PcActor actor) =>
        _actions != null && await _actions.HasActionAsync(actor.Roles, PartCatalogueService.Module, "Admin");

    private static PcImportResult Denied(string kind)
    {
        var r = new PcImportResult { Kind = kind };
        r.Errors.Add("Pc_Err_NotAuthorized");
        return r;
    }

    public async Task<PcImportResult> ImportTechnicalClassesAsync(Stream xlsx, PcActor actor) =>
        await IsAdminAsync(actor) ? await ImportTechnicalClassesAsync(xlsx, updateExisting: true) : Denied("TechnicalClass");

    public async Task<PcImportResult> ImportCommodityGroupsAsync(Stream xlsx, PcActor actor) =>
        await IsAdminAsync(actor) ? await ImportCommodityGroupsAsync(xlsx, updateExisting: true) : Denied("CommodityGroup");

    public async Task<PcImportResult> ImportProductFamiliesAsync(Stream xlsx, PcActor actor) =>
        await IsAdminAsync(actor) ? await ImportProductFamiliesAsync(xlsx, updateExisting: true) : Denied("ProductFamily");

    public async Task<PcImportResult> ImportNameListAsync(string kind, Stream xlsx, PcActor actor) =>
        await IsAdminAsync(actor) ? await ImportNameListAsync(kind, xlsx, updateExisting: true) : Denied(kind);

    /// <summary>سلول → رشته: اعداد صحیح بدون اعشار/نماد علمی (مثلاً 10100)، متن Trim‌شده.</summary>
    internal static string Cell(IXLCell c)
    {
        if (c.IsEmpty()) return string.Empty;
        if (c.DataType == XLDataType.Number)
        {
            var d = c.GetDouble();
            return d == Math.Floor(d) ? ((long)d).ToString() : d.ToString("0.#########");
        }
        return c.GetString().Trim();
    }

    public async Task<PcImportResult> ImportTechnicalClassesAsync(Stream xlsx, bool updateExisting)
    {
        var sw = Stopwatch.StartNew();
        var res = new PcImportResult { Kind = "TechnicalClass" };
        var existing = await _ctx.PartCatalogueTechnicalClasses.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var wb = new XLWorkbook(xlsx))
        {
            var used = wb.Worksheets.First().RangeUsed();
            var rows = used == null ? new List<IXLRangeRow>() : used.Rows().Skip(1).Take(MaxRows + 1).ToList();
            if (rows.Count > MaxRows) { res.Errors.Add($"Too many rows (> {MaxRows})"); return res; }

            foreach (var row in rows)
            {
                var code = Cell(row.Cell(1)); var desc = Cell(row.Cell(2)); var lng = Cell(row.Cell(3));
                if (code.Length == 0 || desc.Length == 0) { res.Skipped++; continue; }
                if (code.Length > 30 || desc.Length > 200) { res.Skipped++; res.Errors.Add($"Row {row.RowNumber()}: code/description too long"); continue; }
                if (!seen.Add(code)) { res.Skipped++; continue; }   // تکراری داخل فایل

                var lngOrNull = lng.Length == 0 ? null : lng;
                if (existing.TryGetValue(code, out var e))
                {
                    if (!updateExisting) { res.Unchanged++; continue; }
                    if (e.Description == desc && e.LongDescription == lngOrNull && e.IsActive) { res.Unchanged++; continue; }
                    e.Description = desc; e.LongDescription = lngOrNull; e.IsActive = true; res.Updated++;
                }
                else
                {
                    _ctx.PartCatalogueTechnicalClasses.Add(new PartCatalogueTechnicalClass { Code = code, Description = desc, LongDescription = lngOrNull });
                    res.Added++;
                }
            }
        }

        await _ctx.SaveChangesAsync();
        res.ElapsedMs = sw.ElapsedMilliseconds;
        return res;
    }

    public async Task<PcImportResult> ImportCommodityGroupsAsync(Stream xlsx, bool updateExisting)
    {
        var sw = Stopwatch.StartNew();
        var res = new PcImportResult { Kind = "CommodityGroup" };
        var existing = await _ctx.PartCatalogueCommodityGroups.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var wb = new XLWorkbook(xlsx))
        {
            var used = wb.Worksheets.First().RangeUsed();
            foreach (var row in (used == null ? new List<IXLRangeRow>() : used.Rows().Skip(1).Take(MaxRows)))
            {
                var code = Cell(row.Cell(1)); var desc = Cell(row.Cell(2));
                if (code.Length == 0 || desc.Length == 0) { res.Skipped++; continue; }
                if (code.Length > 30 || desc.Length > 200) { res.Skipped++; res.Errors.Add($"Row {row.RowNumber()}: too long"); continue; }
                if (!seen.Add(code)) { res.Skipped++; continue; }

                if (existing.TryGetValue(code, out var e))
                {
                    if (!updateExisting || (e.Description == desc && e.IsActive)) { res.Unchanged++; continue; }
                    e.Description = desc; e.IsActive = true; e.NeedsConfirmation = false; res.Updated++;
                }
                else { _ctx.PartCatalogueCommodityGroups.Add(new PartCatalogueCommodityGroup { Code = code, Description = desc }); res.Added++; }
            }
        }

        await _ctx.SaveChangesAsync();
        res.ElapsedMs = sw.ElapsedMilliseconds;
        return res;
    }

    public async Task<PcImportResult> ImportProductFamiliesAsync(Stream xlsx, bool updateExisting)
    {
        var sw = Stopwatch.StartNew();
        var res = new PcImportResult { Kind = "ProductFamily" };
        var existing = await _ctx.PartCatalogueProductFamilies.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var wb = new XLWorkbook(xlsx))
        {
            var used = wb.Worksheets.First().RangeUsed();
            foreach (var row in (used == null ? new List<IXLRangeRow>() : used.Rows().Skip(1).Take(MaxRows)))
            {
                var code = Cell(row.Cell(1)); var desc = Cell(row.Cell(2));   // کد ممکن است متنی باشد: '*', ASPC, IPCC, SPME
                if (code.Length == 0 || desc.Length == 0) { res.Skipped++; continue; }
                if (code.Length > 30 || desc.Length > 200) { res.Skipped++; res.Errors.Add($"Row {row.RowNumber()}: too long"); continue; }
                if (!seen.Add(code)) { res.Skipped++; continue; }

                if (existing.TryGetValue(code, out var e))
                {
                    if (!updateExisting || (e.Description == desc && e.IsActive)) { res.Unchanged++; continue; }
                    e.Description = desc; e.IsActive = true; res.Updated++;
                }
                else { _ctx.PartCatalogueProductFamilies.Add(new PartCatalogueProductFamily { Code = code, Description = desc }); res.Added++; }
            }
        }

        await _ctx.SaveChangesAsync();
        res.ElapsedMs = sw.ElapsedMilliseconds;
        return res;
    }

    public async Task<PcImportResult> ImportNameListAsync(string kind, Stream xlsx, bool updateExisting)
    {
        var sw = Stopwatch.StartNew();
        var isMfr = string.Equals(kind, "Manufacturer", StringComparison.OrdinalIgnoreCase);
        if (!isMfr && !string.Equals(kind, "Supplier", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentOutOfRangeException(nameof(kind));

        var res = new PcImportResult { Kind = isMfr ? "Manufacturer" : "Supplier" };
        var names = new List<string>();

        using (var wb = new XLWorkbook(xlsx))
        {
            var used = wb.Worksheets.First().RangeUsed();
            if (used != null)
                foreach (var row in used.Rows().Take(MaxRows))
                {
                    var n = Cell(row.Cell(1));
                    if (n.Length == 0) continue;
                    // ردیف عنوان (اولین سطر) اگر خودش یکی از واژه‌های عنوان باشد حذف می‌شود
                    if (row.RowNumber() == used.FirstRow().RowNumber()
                        && (n.Equals("Name", StringComparison.OrdinalIgnoreCase) || n.Equals("Manufacturer", StringComparison.OrdinalIgnoreCase)
                            || n.Equals("Supplier", StringComparison.OrdinalIgnoreCase) || n.Equals("Supplier Name", StringComparison.OrdinalIgnoreCase)
                            || n.Equals("Manufacturer Name", StringComparison.OrdinalIgnoreCase)))
                        continue;
                    names.Add(n);
                }
        }

        var distinct = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        res.Skipped += names.Count - distinct.Count;

        if (isMfr)
        {
            var existing = await _ctx.PartCatalogueManufacturers.ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var n in distinct)
            {
                if (n.Length > 200) { res.Skipped++; continue; }
                var wrong = PartCatalogueRules.IsWrongName(n);
                var local = PartCatalogueRules.IsLocalName(n);
                if (existing.TryGetValue(n, out var e))
                {
                    if (!updateExisting || (e.IsWrong == wrong && e.IsLocal == local)) { res.Unchanged++; continue; }
                    e.IsWrong = wrong; e.IsLocal = local; res.Updated++;
                }
                else
                {
                    _ctx.PartCatalogueManufacturers.Add(new PartCatalogueManufacturer
                    { Name = n, NormalizedName = PartCatalogueRules.NormalizeName(n), IsLocal = local, IsWrong = wrong });
                    res.Added++;
                }
            }
        }
        else
        {
            var existing = await _ctx.PartCatalogueSuppliers.ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var n in distinct)
            {
                if (n.Length > 300) { res.Skipped++; continue; }
                if (existing.ContainsKey(n)) { res.Unchanged++; continue; }
                _ctx.PartCatalogueSuppliers.Add(new PartCatalogueSupplier { Name = n, NormalizedName = PartCatalogueRules.NormalizeName(n) });
                res.Added++;
            }
        }

        await _ctx.SaveChangesAsync();
        res.ElapsedMs = sw.ElapsedMilliseconds;
        return res;
    }

    // ------------------------------------------------------------------ مدیریت (فقط PartCatalogue.Admin)

    public async Task<PcOverviewResult> GetOverviewAsync(PcActor actor)
    {
        if (!await IsAdminAsync(actor)) return new PcOverviewResult { Allowed = false };

        var names = await _ctx.Employees.AsNoTracking()
            .Select(e => new { e.EmployeeId, Name = (e.FirstName + " " + e.LastName).Trim() })
            .ToDictionaryAsync(e => e.EmployeeId, e => e.Name);
        string Emp(int id) => names.TryGetValue(id, out var n) ? n : "#" + id;

        var groups = await _ctx.PartCatalogueCommodityGroups.AsNoTracking()
            .ToDictionaryAsync(g => g.CommodityGroupId, g => g.Description + "-" + g.Code);
        var maps = await _ctx.PartCatalogueSectionCommodityGroups.AsNoTracking().ToListAsync();
        var assigns = await _ctx.PartCatalogueApproverAssignments.AsNoTracking().Where(a => a.IsActive).ToListAsync();

        var ov = new PcAdminOverview
        {
            Sections = (await _ctx.PartCatalogueSections.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync()).Select(x => new PcAdminSectionRow
            {
                SectionId = x.SectionId, Code = x.Code, NameFa = x.NameFa, RequiresSparePartsReview = x.RequiresSparePartsReview,
                RoutingNeedsConfirmation = x.RoutingNeedsConfirmation, IsActive = x.IsActive,
                Groups = maps.Where(m => m.SectionId == x.SectionId)
                    .Select(m => new PcAdminGroupMap(m.SectionCommodityGroupId, groups.TryGetValue(m.CommodityGroupId, out var g) ? g : "?", m.NeedsConfirmation)).ToList()
            }).ToList(),
            Steps = (await _ctx.PartCatalogueWorkflowSteps.AsNoTracking().OrderBy(x => x.StepOrder).ToListAsync()).Select(x => new PcAdminStepRow
            {
                StepId = x.WorkflowStepId, StepKey = x.StepKey, Order = x.StepOrder, NameEn = x.NameEn, NameFa = x.NameFa, Kind = x.Kind,
                Source = x.ApproverSource, Condition = x.ConditionKey, IsActive = x.IsActive, NeedsConfirmation = x.NeedsConfirmation,
                Approvers = assigns.Where(a => a.StepKey == x.StepKey).Select(a => new PcAdminApprover(a.ApproverAssignmentId, Emp(a.EmployeeId))).ToList()
            }).ToList(),
            Ceo = assigns.Where(a => a.StepKey == PartCatalogueApproverAssignment.CeoKey)
                .Select(a => new PcAdminApprover(a.ApproverAssignmentId, Emp(a.EmployeeId))).ToList(),
            OrgLinks = (await _ctx.PartCatalogueOrgLinks.AsNoTracking().ToListAsync())
                .Select(l => (l.OrgLinkId, Emp(l.EmployeeId), Emp(l.ManagerEmployeeId))).OrderBy(l => l.Item2).ToList(),
            Group2Maps = (await _ctx.PartCatalogueCommodityGroupChildren.AsNoTracking().ToListAsync())
                .Select(m => (m.CommodityGroupChildId,
                    groups.TryGetValue(m.ParentGroupId, out var pg) ? pg : "?", groups.TryGetValue(m.ChildGroupId, out var cg) ? cg : "?", m.NeedsConfirmation)).ToList(),
            Counts = new Dictionary<string, int>
            {
                ["TechnicalClass"] = await _ctx.PartCatalogueTechnicalClasses.CountAsync(),
                ["CommodityGroup"] = await _ctx.PartCatalogueCommodityGroups.CountAsync(),
                ["ProductFamily"] = await _ctx.PartCatalogueProductFamilies.CountAsync(),
                ["Manufacturer"] = await _ctx.PartCatalogueManufacturers.CountAsync(),
                ["Supplier"] = await _ctx.PartCatalogueSuppliers.CountAsync(),
            }
        };
        return new PcOverviewResult { Allowed = true, Overview = ov };
    }

    public async Task<PcResult> SetSectionAsync(int sectionId, bool requiresSparePartsReview, bool isActive, PcActor actor)
    {
        if (!await IsAdminAsync(actor)) return PcResult.Fail("Id", "Pc_Err_NotAuthorized");
        var sec = await _ctx.PartCatalogueSections.FirstOrDefaultAsync(x => x.SectionId == sectionId);
        if (sec == null) return PcResult.Fail("Id", "Pc_Err_NotFound");
        sec.RequiresSparePartsReview = requiresSparePartsReview;
        sec.IsActive = isActive;
        sec.RoutingNeedsConfirmation = false;   // مدیر صریحاً تنظیم کرد
        await _ctx.SaveChangesAsync();
        return PcResult.Ok();
    }

    public async Task<PcResult> AddSectionGroupAsync(int sectionId, int commodityGroupId, PcActor actor)
    {
        if (!await IsAdminAsync(actor)) return PcResult.Fail("Id", "Pc_Err_NotAuthorized");
        if (!await _ctx.PartCatalogueSections.AnyAsync(x => x.SectionId == sectionId)
            || !await _ctx.PartCatalogueCommodityGroups.AnyAsync(x => x.CommodityGroupId == commodityGroupId))
            return PcResult.Fail("Id", "Pc_Err_InvalidReference");
        if (await _ctx.PartCatalogueSectionCommodityGroups.AnyAsync(x => x.SectionId == sectionId && x.CommodityGroupId == commodityGroupId))
            return PcResult.Fail("Id", "Pc_Err_AlreadyExists");
        _ctx.PartCatalogueSectionCommodityGroups.Add(new PartCatalogueSectionCommodityGroup { SectionId = sectionId, CommodityGroupId = commodityGroupId });
        await _ctx.SaveChangesAsync();
        return PcResult.Ok();
    }

    public async Task<PcResult> RemoveSectionGroupAsync(int mapId, PcActor actor)
    {
        if (!await IsAdminAsync(actor)) return PcResult.Fail("Id", "Pc_Err_NotAuthorized");
        var m = await _ctx.PartCatalogueSectionCommodityGroups.FirstOrDefaultAsync(x => x.SectionCommodityGroupId == mapId);
        if (m == null) return PcResult.Fail("Id", "Pc_Err_NotFound");
        _ctx.PartCatalogueSectionCommodityGroups.Remove(m);
        await _ctx.SaveChangesAsync();
        return PcResult.Ok();
    }

    public async Task<PcResult> AddGroupChildAsync(int parentGroupId, int childGroupId, PcActor actor)
    {
        if (!await IsAdminAsync(actor)) return PcResult.Fail("Id", "Pc_Err_NotAuthorized");
        if (!await _ctx.PartCatalogueCommodityGroups.AnyAsync(x => x.CommodityGroupId == parentGroupId)
            || !await _ctx.PartCatalogueCommodityGroups.AnyAsync(x => x.CommodityGroupId == childGroupId))
            return PcResult.Fail("Id", "Pc_Err_InvalidReference");
        if (await _ctx.PartCatalogueCommodityGroupChildren.AnyAsync(x => x.ParentGroupId == parentGroupId && x.ChildGroupId == childGroupId))
            return PcResult.Fail("Id", "Pc_Err_AlreadyExists");
        _ctx.PartCatalogueCommodityGroupChildren.Add(new PartCatalogueCommodityGroupChild { ParentGroupId = parentGroupId, ChildGroupId = childGroupId });
        await _ctx.SaveChangesAsync();
        return PcResult.Ok();
    }

    public async Task<PcResult> RemoveGroupChildAsync(int mapId, PcActor actor)
    {
        if (!await IsAdminAsync(actor)) return PcResult.Fail("Id", "Pc_Err_NotAuthorized");
        var m = await _ctx.PartCatalogueCommodityGroupChildren.FirstOrDefaultAsync(x => x.CommodityGroupChildId == mapId);
        if (m == null) return PcResult.Fail("Id", "Pc_Err_NotFound");
        _ctx.PartCatalogueCommodityGroupChildren.Remove(m);
        await _ctx.SaveChangesAsync();
        return PcResult.Ok();
    }

    public async Task<PcResult> UpdateStepAsync(int stepId, int order, bool isActive, string conditionKey, PcActor actor)
    {
        if (!await IsAdminAsync(actor)) return PcResult.Fail("Id", "Pc_Err_NotAuthorized");
        if (!PartCatalogueConditions.All.Contains(conditionKey)) return PcResult.Fail("ConditionKey", "Pc_Err_InvalidReference");
        if (order < 1 || order > 1000) return PcResult.Fail("Order", "Pc_Err_Negative");
        var st = await _ctx.PartCatalogueWorkflowSteps.FirstOrDefaultAsync(x => x.WorkflowStepId == stepId);
        if (st == null) return PcResult.Fail("Id", "Pc_Err_NotFound");
        st.StepOrder = order;
        st.IsActive = isActive;
        st.ConditionKey = conditionKey;
        st.NeedsConfirmation = false;
        await _ctx.SaveChangesAsync();
        return PcResult.Ok();
    }

    public async Task<PcResult> AddApproverAsync(string stepKey, int employeeId, PcActor actor)
    {
        if (!await IsAdminAsync(actor)) return PcResult.Fail("Id", "Pc_Err_NotAuthorized");
        var validKey = stepKey == PartCatalogueApproverAssignment.CeoKey
                       || await _ctx.PartCatalogueWorkflowSteps.AnyAsync(x => x.StepKey == stepKey && x.ApproverSource == PartCatalogueApproverSources.AssignedPool);
        if (!validKey || !await _ctx.Employees.AnyAsync(e => e.EmployeeId == employeeId)) return PcResult.Fail("Id", "Pc_Err_InvalidReference");

        var existing = await _ctx.PartCatalogueApproverAssignments.FirstOrDefaultAsync(a => a.StepKey == stepKey && a.EmployeeId == employeeId);
        if (existing != null)
        {
            if (existing.IsActive) return PcResult.Fail("Id", "Pc_Err_AlreadyExists");
            existing.IsActive = true;
        }
        else _ctx.PartCatalogueApproverAssignments.Add(new PartCatalogueApproverAssignment { StepKey = stepKey, EmployeeId = employeeId });

        await _ctx.SaveChangesAsync();
        return PcResult.Ok();
    }

    public async Task<PcResult> RemoveApproverAsync(int assignmentId, PcActor actor)
    {
        if (!await IsAdminAsync(actor)) return PcResult.Fail("Id", "Pc_Err_NotAuthorized");
        var a = await _ctx.PartCatalogueApproverAssignments.FirstOrDefaultAsync(x => x.ApproverAssignmentId == assignmentId);
        if (a == null) return PcResult.Fail("Id", "Pc_Err_NotFound");
        a.IsActive = false;   // غیرفعال‌سازی (تاریخچه‌ی تصمیم‌ها به نام همان فرد می‌ماند)
        await _ctx.SaveChangesAsync();
        return PcResult.Ok();
    }

    public async Task<PcResult> SetOrgLinkAsync(int employeeId, int managerEmployeeId, PcActor actor)
    {
        if (!await IsAdminAsync(actor)) return PcResult.Fail("Id", "Pc_Err_NotAuthorized");
        if (employeeId == managerEmployeeId) return PcResult.Fail("ManagerEmployeeId", "Pc_Err_SelfManager");
        if (!await _ctx.Employees.AnyAsync(e => e.EmployeeId == employeeId) || !await _ctx.Employees.AnyAsync(e => e.EmployeeId == managerEmployeeId))
            return PcResult.Fail("Id", "Pc_Err_InvalidReference");

        // جلوگیری از حلقه: مدیر نباید (مستقیم/غیرمستقیم) زیردستِ همین کارمند باشد
        var cursor = (int?)managerEmployeeId;
        for (var hops = 0; cursor.HasValue && hops < 50; hops++)
        {
            if (cursor.Value == employeeId) return PcResult.Fail("ManagerEmployeeId", "Pc_Err_OrgCycle");
            var current = cursor.Value;
            cursor = await _ctx.PartCatalogueOrgLinks.Where(l => l.EmployeeId == current).Select(l => (int?)l.ManagerEmployeeId).FirstOrDefaultAsync();
        }

        var link = await _ctx.PartCatalogueOrgLinks.FirstOrDefaultAsync(l => l.EmployeeId == employeeId);
        if (link == null) _ctx.PartCatalogueOrgLinks.Add(new PartCatalogueOrgLink { EmployeeId = employeeId, ManagerEmployeeId = managerEmployeeId });
        else link.ManagerEmployeeId = managerEmployeeId;

        await _ctx.SaveChangesAsync();
        return PcResult.Ok();
    }

    public async Task<PcResult> RemoveOrgLinkAsync(int linkId, PcActor actor)
    {
        if (!await IsAdminAsync(actor)) return PcResult.Fail("Id", "Pc_Err_NotAuthorized");
        var l = await _ctx.PartCatalogueOrgLinks.FirstOrDefaultAsync(x => x.OrgLinkId == linkId);
        if (l == null) return PcResult.Fail("Id", "Pc_Err_NotFound");
        _ctx.PartCatalogueOrgLinks.Remove(l);
        await _ctx.SaveChangesAsync();
        return PcResult.Ok();
    }

    public async Task<List<PcLookupItem>> SearchEmployeesAsync(string? q, int take = 20)
    {
        q = q?.Trim();
        var query = _ctx.Employees.AsNoTracking().Where(e => e.IsActive);
        if (!string.IsNullOrEmpty(q))
            query = query.Where(e => e.FirstName.Contains(q) || e.LastName.Contains(q) || (e.PersonnelCode != null && e.PersonnelCode.Contains(q)));

        var rows = await query.OrderBy(e => e.LastName).ThenBy(e => e.FirstName).Take(Math.Clamp(take, 1, 50))
            .Select(e => new { e.EmployeeId, e.FirstName, e.LastName, e.PersonnelCode }).ToListAsync();
        return rows.Select(e => new PcLookupItem
        {
            Id = e.EmployeeId, Code = e.PersonnelCode ?? "",
            Text = (e.FirstName + " " + e.LastName).Trim() + (e.PersonnelCode != null ? " (" + e.PersonnelCode + ")" : "")
        }).ToList();
    }
}
