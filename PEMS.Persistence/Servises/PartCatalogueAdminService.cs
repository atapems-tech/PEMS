using System.Diagnostics;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ActorResolution;
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
    private readonly IEnterpriseActorResolver? _resolver;

    /// <param name="actions">null فقط برای Seed داخلی (بدون Actor)؛ در DI همیشه مقدار دارد.</param>
    public PartCatalogueAdminService(PEMSDbContext ctx, IRoleModuleActionService? actions = null, IEnterpriseActorResolver? resolver = null)
    {
        _ctx = ctx;
        _actions = actions;
        _resolver = resolver;
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

        var positions = await _ctx.Positions.AsNoTracking().Where(p => p.IsActive)
            .Select(p => new { p.PositionId, p.Title, Team = p.Team != null ? p.Team.Name : null }).OrderBy(p => p.Title).ToListAsync();
        var posTitle = positions.ToDictionary(p => p.PositionId, p => p.Title + (p.Team != null ? " (" + p.Team + ")" : ""));
        var respRows = await _ctx.WorkflowActorResponsibilities.AsNoTracking().Include(r => r.Positions).OrderBy(r => r.WorkflowActorResponsibilityId).ToListAsync();
        var cgr = await _ctx.CommodityGroupResponsibles.AsNoTracking().OrderBy(x => x.ResponsibilityKey).ThenBy(x => x.CommodityGroupId).ToListAsync();

        var binding = await _ctx.WorkflowBindings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Module == "PartCatalogue" && b.EntityType == "PartCatalogueRequest" && b.Context == "Approval");
        var stageRows = new List<PcAdminWorkflowStage>();
        if (binding != null)
        {
            var versionId = binding.PinnedVersionId ?? await _ctx.WorkflowVersions.AsNoTracking()
                .Where(v => v.WorkflowDefinitionId == binding.WorkflowDefinitionId && v.Status == "Published").OrderByDescending(v => v.VersionNumber)
                .Select(v => (int?)v.WorkflowVersionId).FirstOrDefaultAsync();
            if (versionId.HasValue)
                stageRows = (await _ctx.WorkflowStages.AsNoTracking().Where(s => s.WorkflowVersionId == versionId).OrderBy(s => s.StageOrder).ToListAsync())
                    .Select(s => new PcAdminWorkflowStage(s.Name, s.StageOrder, s.ResponsibilityKey, s.EntryCondition)).ToList();
        }

        var findings = _resolver == null ? new List<PcAdminFinding>()
            : (await _resolver.GetDataQualityAsync()).Select(f => new PcAdminFinding(f.Severity, f.Code, f.ResponsibilityKey, f.Message)).ToList();

        var ov = new PcAdminOverview
        {
            Responsibilities = respRows.Select(r => new PcAdminResponsibilityRow
            {
                Id = r.WorkflowActorResponsibilityId, Key = r.ResponsibilityKey, DisplayName = r.DisplayName, DisplayNameFa = r.DisplayNameFa, Kind = r.Kind,
                RequiredAction = r.RequiredAction, RoleName = r.RoleName, ScopeType = r.ScopeType, ScopeValue = r.ScopeValue, ManagerLevel = r.ManagerLevel,
                Strategy = r.SelectionStrategy, IsActive = r.IsActive,
                Positions = r.Positions.OrderBy(p => p.Priority).ThenBy(p => p.PositionId)
                    .Select(p => new PcAdminPositionRef(p.PositionId, posTitle.TryGetValue(p.PositionId, out var t) ? t : "#" + p.PositionId, p.Priority)).ToList()
            }).ToList(),
            CommodityResponsibles = cgr.Select(x => new PcAdminCommodityResponsible(x.CommodityGroupResponsibleId, x.ResponsibilityKey,
                groups.TryGetValue(x.CommodityGroupId, out var gtxt) ? gtxt : "?",
                x.PositionId.HasValue && posTitle.TryGetValue(x.PositionId.Value, out var pt) ? pt : null,
                x.EmployeeId.HasValue ? Emp(x.EmployeeId.Value) : null, x.Priority, x.IsActive)).ToList(),
            Positions = positions.Select(p => new PcLookupItem { Id = p.PositionId, Code = p.Title, Text = posTitle[p.PositionId] }).ToList(),
            Roles = await _ctx.Roles.AsNoTracking().OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync(),
            WorkflowStages = stageRows,
            Findings = findings,
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
        var validKey = await _ctx.PartCatalogueWorkflowSteps.AnyAsync(x => x.StepKey == stepKey && x.ApproverSource == PartCatalogueApproverSources.AssignedPool);
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

    // ------------------------------------------------------------------ Enterprise Actor Resolution (فقط PartCatalogue.Admin)

    public async Task<PcResult> SaveResponsibilityAsync(PcResponsibilityInput i, PcActor actor)
    {
        if (!await IsAdminAsync(actor)) return PcResult.Fail("Id", "Pc_Err_NotAuthorized");

        var r = await _ctx.WorkflowActorResponsibilities.Include(x => x.Positions).FirstOrDefaultAsync(x => x.WorkflowActorResponsibilityId == i.Id);
        if (r == null) return PcResult.Fail("Id", "Pc_Err_NotFound");

        if (!Domain.Entities.ActorResponsibilityKinds.All.Contains(i.Kind)) return PcResult.Fail("Kind", "Pc_Err_InvalidReference");
        if (!Domain.Entities.ActorResponsibilityScopes.All.Contains(i.ScopeType)) return PcResult.Fail("ScopeType", "Pc_Err_InvalidReference");
        if (i.SelectionStrategy is not (Domain.Entities.ActorSelectionStrategies.Queue or Domain.Entities.ActorSelectionStrategies.SingleActor))
            return PcResult.Fail("SelectionStrategy", "Pc_Err_InvalidReference");

        var scopeIds = i.ScopeType == Domain.Entities.ActorResponsibilityScopes.None ? new HashSet<int>() : PEMS.Persistence.Services.EnterpriseActorResolver.ParseIds(i.ScopeValue);
        if (i.ScopeType != Domain.Entities.ActorResponsibilityScopes.None && scopeIds.Count == 0) return PcResult.Fail("ScopeValue", "Pc_Err_Required");

        var roleName = string.IsNullOrWhiteSpace(i.RoleName) ? null : i.RoleName.Trim();
        if (roleName != null && !await _ctx.Roles.AnyAsync(x => x.Name == roleName)) return PcResult.Fail("RoleName", "Pc_Err_InvalidReference");

        var posIds = (i.PositionIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).Distinct().ToList();
        if (posIds.Count > 0 && await _ctx.Positions.CountAsync(p => posIds.Contains(p.PositionId)) != posIds.Count)
            return PcResult.Fail("PositionIds", "Pc_Err_InvalidReference");
        if (i.Kind == Domain.Entities.ActorResponsibilityKinds.RequesterManager && i.ManagerLevel is not (1 or 2))
            return PcResult.Fail("ManagerLevel", "Pc_Err_InvalidReference");

        r.Kind = i.Kind;
        r.RequiredModule = string.IsNullOrWhiteSpace(i.RequiredAction) ? null : "PartCatalogue";
        r.RequiredAction = string.IsNullOrWhiteSpace(i.RequiredAction) ? null : i.RequiredAction.Trim();
        r.RoleName = roleName;
        r.ScopeType = i.ScopeType;
        r.ScopeValue = i.ScopeType == Domain.Entities.ActorResponsibilityScopes.None ? null : string.Join(",", scopeIds.OrderBy(x => x));
        r.ManagerLevel = i.Kind == Domain.Entities.ActorResponsibilityKinds.RequesterManager ? i.ManagerLevel : null;
        r.SelectionStrategy = i.SelectionStrategy;
        r.IsActive = i.IsActive;

        _ctx.WorkflowActorResponsibilityPositions.RemoveRange(r.Positions.ToList());
        r.Positions.Clear();
        for (var n = 0; n < posIds.Count; n++)
            r.Positions.Add(new Domain.Entities.WorkflowActorResponsibilityPosition { PositionId = posIds[n], Priority = n + 1 });

        await _ctx.SaveChangesAsync();
        return PcResult.Ok();
    }

    public async Task<PcResult> AddCommodityResponsibleAsync(string responsibilityKey, int commodityGroupId, int? positionId, int? employeeId, int priority, PcActor actor)
    {
        if (!await IsAdminAsync(actor)) return PcResult.Fail("Id", "Pc_Err_NotAuthorized");
        if (!positionId.HasValue && !employeeId.HasValue) return PcResult.Fail("PositionId", "Pc_Err_Required");
        if (!await _ctx.WorkflowActorResponsibilities.AnyAsync(x => x.ResponsibilityKey == responsibilityKey && x.Kind == Domain.Entities.ActorResponsibilityKinds.CommodityPosition))
            return PcResult.Fail("Id", "Pc_Err_InvalidReference");
        if (!await _ctx.PartCatalogueCommodityGroups.AnyAsync(x => x.CommodityGroupId == commodityGroupId)) return PcResult.Fail("CommodityGroupId", "Pc_Err_InvalidReference");
        if (positionId.HasValue && !await _ctx.Positions.AnyAsync(x => x.PositionId == positionId)) return PcResult.Fail("PositionId", "Pc_Err_InvalidReference");
        if (employeeId.HasValue && !await _ctx.Employees.AnyAsync(x => x.EmployeeId == employeeId)) return PcResult.Fail("EmployeeId", "Pc_Err_InvalidReference");

        var dup = await _ctx.CommodityGroupResponsibles.FirstOrDefaultAsync(x => x.ResponsibilityKey == responsibilityKey && x.CommodityGroupId == commodityGroupId
            && x.PositionId == positionId && x.EmployeeId == employeeId);
        if (dup != null) { dup.IsActive = true; dup.Priority = priority; }
        else
            _ctx.CommodityGroupResponsibles.Add(new Domain.Entities.CommodityGroupResponsible
            { ResponsibilityKey = responsibilityKey, CommodityGroupId = commodityGroupId, PositionId = positionId, EmployeeId = employeeId, Priority = priority });
        await _ctx.SaveChangesAsync();
        return PcResult.Ok();
    }

    /// <summary>حذف نرم (IsActive=false): سابقه‌ی پیکربندی برای ممیزی حفظ می‌شود.</summary>
    public async Task<PcResult> RemoveCommodityResponsibleAsync(int id, PcActor actor)
    {
        if (!await IsAdminAsync(actor)) return PcResult.Fail("Id", "Pc_Err_NotAuthorized");
        var row = await _ctx.CommodityGroupResponsibles.FirstOrDefaultAsync(x => x.CommodityGroupResponsibleId == id);
        if (row == null) return PcResult.Fail("Id", "Pc_Err_NotFound");
        row.IsActive = false;
        await _ctx.SaveChangesAsync();
        return PcResult.Ok();
    }

    public async Task<ActorResolutionResult?> PreviewResolutionAsync(string responsibilityKey, int requesterEmployeeId, int? commodityGroupId, PcActor actor)
    {
        if (_resolver == null || !await IsAdminAsync(actor)) return null;
        var ctx = new Dictionary<string, string?>
        {
            [ActorContextKeys.CommodityGroup1Id] = commodityGroupId?.ToString(),
            [ActorContextKeys.CommodityGroup2Id] = null
        };
        return await _resolver.ResolveAsync(new ActorResolutionRequest(responsibilityKey, requesterEmployeeId, ctx));
    }
}
