using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-830: create, edit and file upload of an engineering document as operations (see <see cref="IEngineeringDocumentFormOperations"/>).</summary>
public sealed class EngineeringDocumentFormOperations(IEngineeringDocumentService service, IDocumentNumberingService numbering, PEMSDbContext context, IPermissionService permissions, ICurrentUserService currentUser,
    IAccessControlService access, IFileStorageService storage, IHttpContextAccessor http) : IEngineeringDocumentFormOperations
{
    private string? ClientIp => http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    private async Task<int?> EmployeeIdAsync()
        => currentUser.UserId.HasValue ? await context.Users.Where(u => u.Id == currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync() : null;

    private async Task RecordAsync(string action, int documentId)
    {
        if (currentUser.UserId.HasValue)
            await access.AuthorizeAsync(new AccessControlRequest { UserId = currentUser.UserId.Value, ResourceName = "EngineeringDocuments", ActionName = action, ResourceId = documentId.ToString(), IpAddress = ClientIp });
    }

    private static bool IsDuplicateDocumentNo(DbUpdateException ex) => ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && (sql.Number == 2601 || sql.Number == 2627);

    public async Task<EngDocSaveAnswer> CreateAsync(string? documentNo, bool useManualNumber, string title, string? description, string documentType, int projectId, int disciplineId, string? area, int? equipmentId, int? contractId)
    {
        var document = new EngineeringDocument { DocumentNo = documentNo ?? "", Title = title, Description = description, DocumentType = documentType, ProjectId = projectId, DisciplineId = disciplineId, Area = area, EquipmentId = equipmentId, ContractId = contractId, Status = "Draft", RevisionNo = "00" };
        var creatorEmployeeId = await EmployeeIdAsync();
        document.PreparedById = creatorEmployeeId;
        if (creatorEmployeeId.HasValue)
        {
            var creator = await context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == creatorEmployeeId.Value);
            if (creator != null) document.PreparedBy = creator.FirstName + " " + creator.LastName;
        }
        var canManual = await permissions.HasPermissionAsync(currentUser.Roles, "EngineeringDocuments", "ManualNumber");
        if (!useManualNumber || !canManual || string.IsNullOrWhiteSpace(document.DocumentNo))
            document.DocumentNo = await numbering.GenerateNextNumberAsync(document.ProjectId, document.DisciplineId, document.DocumentType);
        try { await service.AddAsync(document); }
        catch (DbUpdateException ex) when (IsDuplicateDocumentNo(ex)) { return new(false, "این شماره‌ی مدرک قبلاً استفاده شده است.", 0); }

        await context.DocumentRevisions.AddAsync(new DocumentRevision { DocumentId = document.DocumentId, RevisionNo = "00", Status = "Draft", Comment = "Initial Revision", RevisionDate = DateTime.Now, IsActive = true });
        await context.DocumentWorkflows.AddAsync(new DocumentWorkflow { DocumentId = document.DocumentId, FromStatus = "", ToStatus = "Created", ActionType = "Create", Comment = "Document Created", ActionDate = DateTime.Now, IsActive = true });
        await context.SaveChangesAsync();
        await RecordAsync("Create", document.DocumentId);
        return new(true, null, document.DocumentId);
    }

    public async Task<List<EngDocEditRow>> OneAsync(int id)
        => await service.GetEntityByIdAsync(id) is { } d ? [new EngDocEditRow(d.DocumentId, d.DocumentNo, d.DocumentType, d.Title, d.Description, d.ProjectId, d.DisciplineId, d.Area, d.IssuePurpose, d.PreparedBy, d.CheckedBy, d.ApprovedBy, d.EquipmentId, d.ContractId, d.RelatedMRId)] : [];

    public async Task<EngDocSaveAnswer> EditAsync(int id, string documentNo, string documentType, string title, string? description, int projectId, int disciplineId, string? area, string? issuePurpose, string? preparedBy, string? checkedBy, string? approvedBy, int? equipmentId, int? contractId, int? relatedMRId)
    {
        var d = await service.GetEntityByIdAsync(id);
        if (d == null) return new(false, "The document was not found.", 0);
        d.DocumentNo = documentNo; d.DocumentType = documentType; d.Title = title; d.Description = description; d.ProjectId = projectId; d.DisciplineId = disciplineId; d.Area = area; d.IssuePurpose = issuePurpose;
        d.PreparedBy = preparedBy; d.CheckedBy = checkedBy; d.ApprovedBy = approvedBy; d.EquipmentId = equipmentId; d.ContractId = contractId; d.RelatedMRId = relatedMRId; d.ModifiedDate = DateTime.Now;
        try { await service.UpdateAsync(d); }
        catch (PEMS.Application.Exceptions.DocumentFrozenException ex) { return new(false, ex.Message, 0); }
        await RecordAsync("Edit", id);
        return new(true, null, id);
    }

    public async Task<EngDocSaveAnswer> UploadAttachmentAsync(int documentId, OperationFile? file, int? revisionId, string? fileRole)
    {
        if (file == null || file.Bytes.Length == 0) return new(false, "Please select a file", 0);
        if (revisionId.HasValue && !await context.DocumentRevisions.AnyAsync(x => x.RevisionId == revisionId.Value && x.DocumentId == documentId)) return new(false, "رویژن انتخاب‌شده متعلق به این مدرک نیست.", 0);
        string storageKey, fileHash;
        await using (var upload = new MemoryStream(file.Bytes))
        {
            var hashing = new PEMS.Persistence.Services.HashingStream(upload);
            storageKey = await storage.SaveAsync("documents", file.FileName, hashing);
            fileHash = hashing.GetHexHash();
        }
        var firstOfficial = revisionId.HasValue && !await context.DocumentAttachments.AnyAsync(x => x.DocumentRevisionId == revisionId.Value && x.IsActive && x.IsOfficial);
        var attachment = new DocumentAttachment
        {
            DocumentId = documentId, DocumentRevisionId = revisionId, FileRole = string.IsNullOrWhiteSpace(fileRole) ? null : fileRole, IsOfficial = firstOfficial, FileHash = fileHash, FileName = file.FileName,
            FileExtension = Path.GetExtension(file.FileName), FilePath = storageKey, FileSize = file.Bytes.Length, MimeType = file.ContentType, AIStatus = "Pending", UploadDate = DateTime.Now, IsActive = true
        };
        await context.DocumentAttachments.AddAsync(attachment);
        await context.SaveChangesAsync();
        var attachmentId = attachment.AttachmentId;
        Hangfire.BackgroundJob.Enqueue<IDocumentAIIngestionService>(x => x.ProcessAttachmentAsync(attachmentId));
        return new(true, null, documentId);
    }

    private static List<EngDocChoice> Pick<T>(IEnumerable<T> rows, Func<T, string> value, Func<T, string> label) => rows.Select(r => new EngDocChoice(value(r), label(r))).ToList();

    public async Task<List<EngDocChoice>> ProjectsAsync() => Pick(await context.Projects.AsNoTracking().Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync(), x => x.ProjectId.ToString(), x => x.ProjectName);
    public async Task<List<EngDocChoice>> DisciplinesAsync() => Pick(await context.Disciplines.AsNoTracking().Select(x => new { x.DisciplineId, x.Name }).ToListAsync(), x => x.DisciplineId.ToString(), x => x.Name);
    public async Task<List<EngDocChoice>> ContractsAsync() => Pick(await context.Contracts.AsNoTracking().Select(x => new { x.ContractId, x.ContractNo }).ToListAsync(), x => x.ContractId.ToString(), x => x.ContractNo);
    public async Task<List<EngDocChoice>> MaterialRequisitionsAsync() => Pick(await context.MaterialRequisitions.AsNoTracking().Select(x => new { x.MRId, x.MRNumber }).ToListAsync(), x => x.MRId.ToString(), x => x.MRNumber);
    public async Task<List<EngDocChoice>> RevisionsAsync(int documentId)
        => Pick(await context.DocumentRevisions.AsNoTracking().Where(x => x.DocumentId == documentId && x.IsActive).OrderByDescending(x => x.RevisionId).Select(x => new { x.RevisionId, x.RevisionNo, x.Status }).ToListAsync(), x => x.RevisionId.ToString(), x => $"Rev {x.RevisionNo} ({x.Status})");
}
