using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PEMS.Application.DTOs;
using PEMS.Application.Exceptions;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Helpers;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-830: the document pages as operations over the same services as the controllers they replace (see <see cref="IDocumentPagesOperations"/>).</summary>
public sealed class DocumentPagesOperations(
    IEngineeringDocumentService documents, IExcelExportService excel, IVDRService vdr, IManufacturerDataBookService mdb, ITransmittalService transmittals, IEngineeringDocumentIntelligenceService intelligence,
    IAccessControlService access, IPermissionService permissions, ICurrentUserService currentUser, PEMSDbContext context, IStringLocalizer<SharedResource> localizer) : IDocumentPagesOperations
{
    private const int MaxExportRows = 20000;
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private string Loc(string prefix, string? value) => VdrUi.Loc(localizer, prefix, value);
    private static int? Int(string? s) => int.TryParse(s, out var v) ? v : null;

    // ---------------------------------------------------------------- document register (the same discipline scope as DocumentRegisterController)
    private async Task<(bool IsRestricted, int? MyDisciplineId)> ScopeAsync()
    {
        var canViewAll = (await access.AuthorizeForRolesAsync(currentUser.Roles, "EngineeringDocuments", "ViewAllDisciplines")).Allowed;
        var restricted = currentUser.UserId.HasValue && !canViewAll;
        if (!restricted) return (false, null);
        var employeeId = await context.Users.Where(u => u.Id == currentUser.UserId!.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
        if (!employeeId.HasValue) return (false, null);
        var employee = await context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId.Value);
        return employee == null ? (false, null) : (true, employee.DisciplineId);
    }

    private static DocumentRegisterFilter Filter(string? project, string? disciplines, string? status, string? q, int pageSize)
        => new()
        {
            ProjectName = string.IsNullOrWhiteSpace(project) ? null : project,
            Disciplines = (disciplines ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Status = string.IsNullOrWhiteSpace(status) ? null : status,
            Search = string.IsNullOrWhiteSpace(q) ? null : q,
            Page = 1,
            PageSize = pageSize,
        };

    private string StatusText(string? s)
    {
        var l = localizer[DocumentStatusBadge.ResourceKey(s)];
        return l.ResourceNotFound ? (s ?? "") : l.Value;
    }

    public async Task<List<DocRegisterRow>> RegisterListAsync(string? project = null, string? disciplines = null, string? status = null, string? q = null)
    {
        var (restricted, mine) = await ScopeAsync();
        var (items, _) = await documents.GetRegisterAllMatchingAsync(Filter(project, disciplines, status, q, DocumentRegisterFilter.MaxPageSize), restricted, mine, MaxExportRows);
        return items.Select(d => new DocRegisterRow(d.DocumentId, d.DocumentNo, d.Title, d.DocumentType, d.Area, string.IsNullOrWhiteSpace(d.DisciplineName) ? localizer["Dr_Unassigned"].Value : d.DisciplineName, d.ProjectName, d.RevisionNo, StatusText(d.Status),
            d.IsFrozen, d.CommentStatus, d.DelayDays is > 0 ? d.DelayDays : null, d.CreatedDate)).ToList();
    }

    public async Task<List<DocChoice>> RegisterProjectsAsync()
    {
        var (restricted, mine) = await ScopeAsync();
        return (await documents.GetRegisterFilterOptionsAsync(restricted, mine)).Projects.Select(x => new DocChoice(x, x)).ToList();
    }

    public async Task<List<DocChoice>> RegisterDisciplinesAsync()
    {
        var (restricted, mine) = await ScopeAsync();
        return (await documents.GetRegisterFilterOptionsAsync(restricted, mine)).Disciplines.Select(x => new DocChoice(x, x)).ToList();
    }

    public async Task<List<DocChoice>> RegisterStatusesAsync()
    {
        var (restricted, mine) = await ScopeAsync();
        return (await documents.GetRegisterFilterOptionsAsync(restricted, mine)).Statuses.Select(x => new DocChoice(x, StatusText(x))).ToList();
    }

    public async Task<DocFileResult> RegisterExportAsync(string? project = null, string? disciplines = null, string? status = null, string? q = null)
    {
        var (restricted, mine) = await ScopeAsync();
        var (items, total) = await documents.GetRegisterAllMatchingAsync(Filter(project, disciplines, status, q, DocumentRegisterFilter.MaxPageSize), restricted, mine, MaxExportRows);
        if (total > MaxExportRows)
            return new(false, $"تعداد مدارک منطبق با فیلتر ({total}) از سقف امن Export ({MaxExportRows}) بیشتر است. لطفاً فیلتر را محدودتر کنید (مثلاً بر اساس پروژه یا دیسیپلین).", [], "", "");
        return new(true, null, excel.ExportDocumentRegister(items), $"DocumentRegister_{DateTime.Now:yyyyMMdd_HHmm}.xlsx", Xlsx);
    }

    public async Task<DocFileResult> RegisterExportSelectedAsync(List<int> ids)
    {
        var (restricted, mine) = await ScopeAsync();
        var selected = await documents.GetRegisterByIdsAsync(ids ?? [], restricted, mine);
        return new(true, null, excel.ExportDocumentRegister(selected), $"DocumentRegister_Selected_{DateTime.Now:yyyyMMdd_HHmm}.xlsx", Xlsx);
    }

    // ---------------------------------------------------------------- vendor document register
    public async Task<List<VdrListRow>> VdrListAsync(string? projectId = null, string? vendorId = null, string? status = null, string? search = null)
    {
        var (items, _) = await vdr.GetAllMatchingAsync(Int(projectId), Int(vendorId), string.IsNullOrWhiteSpace(status) ? null : status, string.IsNullOrWhiteSpace(search) ? null : search, MaxExportRows);
        return items.Select(i => new VdrListRow(i.VDRItemId, i.DocumentNumber, i.DocumentDescription, i.DocumentTypeCode, i.VendorName, i.ProjectName, i.DisciplineName, i.LatestRevisionNo, i.LatestReviewCategory, Loc("St_", i.WorkflowStatus), i.IsOverdue)).ToList();
    }

    public async Task<List<VdrDashboardRow>> VdrDashboardAsync(string? projectId = null)
    {
        var d = await vdr.GetDashboardAsync(Int(projectId));
        return [new VdrDashboardRow(1, d.TotalItems, d.OverdueItems, d.SlaOverdueItems, (int)Math.Round(d.OnTimePercentage), d.TopDelayedVendors.Count)];
    }

    public async Task<List<DocChoice>> VdrProjectsAsync() => await context.Projects.AsNoTracking().OrderBy(x => x.ProjectName).Select(x => new DocChoice(x.ProjectId.ToString(), x.ProjectName)).ToListAsync();

    public async Task<List<DocChoice>> VdrVendorsAsync() => await context.Vendors.AsNoTracking().OrderBy(x => x.Name).Select(x => new DocChoice(x.VendorId.ToString(), x.Name)).ToListAsync();

    public Task<List<DocChoice>> VdrStatusesAsync()
        => Task.FromResult(new[] { "Planned", "Submitted", "UnderReview", "PendingResubmission", "Closed" }.Select(s => new DocChoice(s, Loc("St_", s))).ToList());

    public async Task<DocFileResult> VdrExportAsync(string? projectId = null, string? vendorId = null, string? status = null, string? search = null)
    {
        var (items, total) = await vdr.GetAllMatchingAsync(Int(projectId), Int(vendorId), string.IsNullOrWhiteSpace(status) ? null : status, string.IsNullOrWhiteSpace(search) ? null : search, MaxExportRows);
        if (total > MaxExportRows) return new(false, $"Export limited to {MaxExportRows:N0} rows; the current filter matches {total:N0}. Narrow the filter and try again.", [], "", "");
        return new(true, null, VdrListExport.Build(items), $"VDR_{DateTime.Now:yyyyMMdd}.xlsx", Xlsx);
    }

    // ---------------------------------------------------------------- manufacturer data books
    private static string Copy(DateTime? d, string yes) => d.HasValue ? yes : "";

    public async Task<List<MdbListRow>> MdbListAsync(string? projectId = null, string? vendorId = null)
        => (await mdb.GetPagedAsync(1, 100000, Int(projectId), Int(vendorId))).Items
            .Select(m => new MdbListRow(m.MDBId, m.EquipmentTagNumber, m.VendorName, m.PoNumber, m.ProjectName, m.PlannedFinalIssueDate, m.IndexOfContentsDueDate, m.IsIndexOverdue, m.Status, Copy(m.HardCopyShippedDate, localizer["Mdb_Shipped"].Value), Copy(m.SoftCopyDeliveredDate, localizer["Mdb_Delivered"].Value))).ToList();

    public async Task<List<DocChoice>> MdbProjectsAsync() => await context.Projects.AsNoTracking().Where(x => x.IsActive).Select(x => new DocChoice(x.ProjectId.ToString(), x.ProjectName)).ToListAsync();

    public async Task<List<DocChoice>> MdbVendorsAsync() => await context.Vendors.AsNoTracking().Where(x => x.IsActive).Select(x => new DocChoice(x.VendorId.ToString(), x.Name)).ToListAsync();

    public async Task<List<MdbHeadRow>> MdbHeadAsync(int id)
    {
        var m = await mdb.GetByIdAsync(id);
        if (m == null) return [];
        return [new MdbHeadRow(m.MDBId, m.EquipmentTagNumber, m.VendorName, m.ProjectName, m.PoNumber, m.Status, m.PlannedFinalIssueDate, m.IndexOfContentsDueDate, m.IsIndexOverdue, m.IndexOfContentsSubmittedDate,
            m.HardCopyShippedDate.HasValue ? $"Shipped {m.HardCopyShippedDate:yyyy-MM-dd} ({m.HardCopyCount} vol)" : "Not shipped", m.SoftCopyDeliveredDate.HasValue ? $"Delivered {m.SoftCopyDeliveredDate:yyyy-MM-dd}" : "Not delivered",
            m.IndexOfContentsSubmittedDate == null, m.Status != "FinalIssued" && m.Status != "Delivered", !m.HardCopyShippedDate.HasValue, !m.SoftCopyDeliveredDate.HasValue)];
    }

    public async Task<List<MdbDocRow>> MdbDocumentsAsync(int id, string part)
    {
        var m = await mdb.GetByIdAsync(id);
        if (m == null) return [];
        var docs = part switch { "A" => m.DocumentsPartA, "B" => m.DocumentsPartB, "C" => m.DocumentsPartC, _ => [] };
        return docs.Select(d => new MdbDocRow(d.MDBDocumentId, d.Part, d.DocumentNo, d.Title, d.DvdVolumeNo, d.IsNotApplicable ? "Not applicable" : "", d.Notes)).ToList();
    }

    public async Task<List<DocChoice>> MdbDocumentOptionsAsync(int id)
    {
        var m = await mdb.GetByIdAsync(id);
        if (m == null) return [];
        return (await context.Documents.AsNoTracking().Where(x => x.IsActive && x.ProjectId == m.ProjectId).OrderByDescending(x => x.CreatedDate).Take(500).Select(x => new { x.DocumentId, x.DocumentNo, x.Title }).ToListAsync())
            .Select(x => new DocChoice(x.DocumentId.ToString(), x.DocumentNo + " - " + x.Title)).ToList();
    }

    // ---------------------------------------------------------------- transmittals
    public async Task<List<TransmittalListRow>> TransmittalListAsync()
        => (await transmittals.GetPagedAsync(1, 100000)).Items.Select(t => new TransmittalListRow(t.TransmittalId, t.TransmittalNo, t.ProjectName, t.From, t.To, Loc("Transmittal_Purpose_", t.Purpose), t.ItemsCount, Loc("St_", t.Status), t.IsResponseOverdue, t.TransmittalDate)).ToList();

    private async Task<bool> Can(string action) => await permissions.HasPermissionAsync(currentUser.Roles, "Transmittal", action);

    public async Task<List<TransmittalHeadRow>> TransmittalHeadAsync(int id)
    {
        var t = await transmittals.GetByIdAsync(id);
        if (t == null) return [];
        var sentLike = t.Status is "Sent" or "Acknowledged";
        return [new TransmittalHeadRow(t.TransmittalId, t.TransmittalNo, Loc("St_", t.Status), t.ProjectName, t.From, t.To, Loc("Transmittal_Purpose_", t.Purpose), t.TransmittalDate, t.SentDate, t.Description,
            t.SupersedesTransmittalNo, t.SupersededByTransmittalNo, t.Status == "Cancelled" ? $"{t.CancelledDate:yyyy-MM-dd} - {t.CancelledReason}" : null,
            t.ResponseRequired, t.ResponseDueDate, t.IsResponseOverdue, t.ResponseReceivedDate, t.ResponseNote,
            t.Status == "Draft" && await Can("Approve"), sentLike && await Can("Delete"), sentLike && t.ResponseRequired && t.ResponseReceivedDate == null && await Can("Edit"), t.Status == "Cancelled" && await Can("Create"), sentLike, t.SupersedesTransmittalId, t.SupersededByTransmittalId)];
    }

    public async Task<List<TransmittalRecipientRow>> TransmittalRecipientsAsync(int id)
    {
        var t = await transmittals.GetByIdAsync(id);
        if (t == null) return [];
        var canAck = t.IsIssued && await Can("Edit");
        return t.Recipients.Select(r => new TransmittalRecipientRow(r.TransmittalRecipientId, r.Name, r.RecipientType, r.Organization, r.ResponseRequired ? localizer["Td_Yes"].Value : "",
            r.AcknowledgedAt.HasValue ? $"{localizer["Td_Acknowledged"].Value} {r.AcknowledgedAt:yyyy-MM-dd} {r.AcknowledgedByName} {r.AcknowledgementNote}".Trim() : localizer["Td_Pending"].Value, canAck && !r.AcknowledgedAt.HasValue)).ToList();
    }

    public async Task<List<TransmittalItemRow>> TransmittalItemsAsync(int id)
    {
        var t = await transmittals.GetByIdAsync(id);
        if (t == null) return [];
        return t.Items.Select(i => new TransmittalItemRow(i.TransmittalItemId, i.DocumentNo, i.DocumentTitle, i.RevisionNo,
            string.IsNullOrEmpty(i.SnapshotFileName) ? localizer["Td_NoFile"].Value : i.SnapshotFileName + (string.IsNullOrEmpty(i.SnapshotFileHash) ? "" : " (" + i.SnapshotFileHash[..Math.Min(8, i.SnapshotFileHash.Length)] + "...)"), i.Remarks)).ToList();
    }

    public async Task<List<DocChoice>> TransmittalProjectsAsync() => await context.Projects.AsNoTracking().Where(x => x.IsActive).Select(x => new DocChoice(x.ProjectId.ToString(), x.ProjectName)).ToListAsync();

    public async Task<List<DocChoice>> TransmittalDocumentsAsync()
        => (await context.EngineeringDocuments.AsNoTracking().Include(x => x.Discipline).Where(x => x.IsActive && !x.IsOnHold)
            .Select(x => new { x.DocumentId, x.DocumentNo, x.Title, x.RevisionNo, DisciplineName = x.Discipline != null ? x.Discipline.Name : "Unassigned", x.Status }).ToListAsync())
            .Select(d => new DocChoice(d.DocumentId.ToString(), $"{d.DocumentNo} - {d.Title} ({localizer["Rev"].Value} {d.RevisionNo} - {(d.DisciplineName == "Unassigned" ? localizer["Unassigned"].Value : d.DisciplineName)} - {localizer[d.Status].Value})")).ToList();

    public async Task<TransmittalCreateAnswer> TransmittalCreateAsync(string transmittalNo, int projectId, string from, string? to, string purpose, string? description, bool responseRequired, string? recipients, List<int> documentIds)
    {
        var list = new List<TransmittalRecipientInput>();
        foreach (var line in (recipients ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = line.Split('|', StringSplitOptions.TrimEntries);
            if (string.IsNullOrWhiteSpace(p[0])) continue;
            var type = p.Length > 1 && p[1] is "External" or "Internal" or "Vendor" or "Client" ? p[1] : "External";
            list.Add(new TransmittalRecipientInput { Name = p[0], RecipientType = type, Organization = p.Length > 2 && p[2].Length > 0 ? p[2] : null, ResponseRequired = p.Length > 3 && p[3] is "yes" or "Yes" or "true" or "1" });
        }
        if (documentIds == null || documentIds.Count == 0) return new(false, localizer["Please select at least one document."].Value, 0);
        if (list.Count == 0 && string.IsNullOrWhiteSpace(to)) return new(false, localizer["Please add at least one recipient."].Value, 0);
        try
        {
            var id = await transmittals.CreateAsync(transmittalNo, projectId, from, to ?? "", purpose, description, documentIds, responseRequired, list);
            return new(true, null, id);
        }
        catch (PemsException ex) when (ex.Code is "transmittal-code-required" or "transmittal-code-duplicate" or "transmittal-documents-held")
        {
            var key = ex.Code switch { "transmittal-code-required" => "Transmittal_CreateCodeRequired", "transmittal-code-duplicate" => "Transmittal_CreateCodeDuplicate", _ => "Transmittal_CreateDocumentsHeld" };
            return new(false, localizer[key].Value, 0);
        }
    }

    // ---------------------------------------------------------------- document control center
    public async Task<List<DccCountsRow>> DccCountsAsync()
    {
        var pending = await context.EngineeringDocuments.CountAsync(x => x.IsActive && x.Status == "Approved");
        var unlinked = await context.Letters.CountAsync(l => l.IsActive && l.LetterType == "Incoming" && l.RelatedDocumentId == null && l.RelatedMRId == null && !l.Links.Any());
        return [new DccCountsRow(1, pending, unlinked)];
    }

    public async Task<List<DccPendingDocRow>> DccPendingIssueAsync()
        => await context.EngineeringDocuments.AsNoTracking().Where(x => x.IsActive && x.Status == "Approved").OrderByDescending(x => x.ModifiedDate).Take(10).Select(x => new DccPendingDocRow(x.DocumentId, x.DocumentNo, x.Title, x.ModifiedDate)).ToListAsync();

    public async Task<List<DccLetterRow>> DccUnlinkedAsync()
        => await context.Letters.AsNoTracking().Where(l => l.IsActive && l.LetterType == "Incoming" && l.RelatedDocumentId == null && l.RelatedMRId == null && !l.Links.Any())
            .OrderByDescending(l => l.LetterDate).Take(15).Select(l => new DccLetterRow(l.LetterId, l.LetterNumber, l.Sender, l.Subject, l.LetterDate, "")).ToListAsync();

    public async Task<List<DccLetterRow>> DccRecentAsync()
        => (await context.Letters.AsNoTracking().Where(l => l.IsActive && l.LetterType == "Incoming").OrderByDescending(l => l.LetterDate).Take(10)
            .Select(l => new { l.LetterId, l.LetterNumber, l.Sender, l.Subject, l.LetterDate, l.RelatedDocumentId, l.RelatedMRId }).ToListAsync())
            .Select(l => new DccLetterRow(l.LetterId, l.LetterNumber, l.Sender, l.Subject, l.LetterDate, l.RelatedDocumentId != null ? localizer["مدرک"].Value : l.RelatedMRId != null ? "MR" : localizer["وصل‌نشده"].Value)).ToList();

    // ---------------------------------------------------------------- upload and intelligence
    public async Task<List<DocChoice>> UploadProjectsAsync() => await context.Projects.AsNoTracking().Where(x => x.IsActive).Select(x => new DocChoice(x.ProjectId.ToString(), x.ProjectName)).ToListAsync();

    public async Task<List<IntelligenceCountsRow>> IntelligenceCountsAsync()
    {
        var d = await intelligence.GetDashboardAsync();
        return [new IntelligenceCountsRow(1, d.TotalDocuments, d.PendingClassificationReview, d.PendingTagLinkReview, d.OpenComments)];
    }

    public async Task<List<IntelligenceStageRow>> IntelligenceStagesAsync()
    {
        var d = await intelligence.GetDashboardAsync();
        var i = 0;
        return new[] { "Uploaded", "AIProcessing", "Classified", "DisciplineAssigned", "UnderReview", "CommentOpen", "Revision", "Approved", "KnowledgeBase" }.Select(s => new IntelligenceStageRow(++i, s, d.ByStage.TryGetValue(s, out var c) ? c : 0)).ToList();
    }

    public async Task<List<IntelligencePendingRow>> IntelligencePendingAsync()
        => (await intelligence.GetPendingReviewAsync()).Select(d => new IntelligencePendingRow(d.DocumentId, d.DocumentNo, d.Title, d.AIWorkflowStage, d.Classifications.Count(c => c.ReviewStatus == "Draft"), d.TagLinks.Count(t => t.IsAIProposed && t.ReviewStatus == "Draft"))).ToList();
}
