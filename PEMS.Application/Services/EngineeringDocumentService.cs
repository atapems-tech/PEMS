using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;

namespace PEMS.Application.Services;

public class EngineeringDocumentService : IEngineeringDocumentService
{
    private readonly IEngineeringDocumentRepository _repository;
    private readonly IKnowledgeBaseService _knowledgeBaseService;


    public EngineeringDocumentService(
        IEngineeringDocumentRepository repository,
        IKnowledgeBaseService knowledgeBaseService)
    {
        _repository = repository;
        _knowledgeBaseService = knowledgeBaseService;
    }



    public async Task<List<EngineeringDocumentDto>> GetAllAsync()
    {
        var documents = await _repository.GetAllAsync();


        return documents.Select(x => new EngineeringDocumentDto
        {
            DocumentId = x.DocumentId,

            DocumentNo = x.DocumentNo,

            Title = x.Title,

            Description = x.Description,

            DocumentType = x.DocumentType,

            ProjectId = x.ProjectId,

            ProjectName = x.Project != null
                ? x.Project.ProjectName
                : "",


            DisciplineId = x.DisciplineId ?? 0,

            DisciplineName = x.Discipline != null
                ? x.Discipline.Name
                : "",


            EquipmentId = x.EquipmentId,

            EquipmentTagNumber = x.Equipment != null ? x.Equipment.TagNumber : null,

            ContractId = x.ContractId,

            ContractNo = x.Contract != null ? x.Contract.ContractNo : null,


            Status = x.Status,

            RevisionNo = x.RevisionNo ?? "00",

            Area = x.Area,

            CommentStatus = x.CommentStatus,

            IsFrozen = x.IsFrozen,

            PlannedDate = x.PlannedDate,

            ActualDate = x.ActualDate,

            DelayDays = x.DelayDays,

            CreatedDate = x.CreatedDate


        }).ToList();
    }


    // طبق Phase E-03 (رفع P1 ممیزی Performance): معادل GetAllAsync اما صفحه‌بندی‌شده و
    // فیلترشده در دیتابیس - Equipment/Contract/Attachments این‌جا Include نشده‌اند (چون
    // GetPagedAsync مخصوص فهرست است و Index.cshtml این فیلدها را نشان نمی‌دهد)، پس مقادیر
    // متناظرشان در DTO خروجی پیش‌فرض/خالی می‌مانند - دقیقاً همان‌طور که در Details از طریق
    // GetByIdAsync جداگانه بارگذاری می‌شوند.
    public async Task<PagedResult<EngineeringDocumentDto>> GetPagedAsync(
        EngineeringDocumentListFilter filter, bool restrictToDiscipline, int? disciplineId)
    {
        var paged = await _repository.GetPagedAsync(filter, restrictToDiscipline, disciplineId);

        return new PagedResult<EngineeringDocumentDto>
        {
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            Items = paged.Items.Select(x => new EngineeringDocumentDto
            {
                DocumentId = x.DocumentId,
                DocumentNo = x.DocumentNo,
                Title = x.Title,
                Description = x.Description,
                DocumentType = x.DocumentType,
                ProjectId = x.ProjectId,
                ProjectName = x.Project?.ProjectName ?? "",
                DisciplineId = x.DisciplineId ?? 0,
                DisciplineName = x.Discipline?.Name ?? "",
                Status = x.Status,
                RevisionNo = x.RevisionNo ?? "00",
                Area = x.Area,
                CommentStatus = x.CommentStatus,
                IsFrozen = x.IsFrozen,
                PlannedDate = x.PlannedDate,
                ActualDate = x.ActualDate,
                DelayDays = x.DelayDays,
                CreatedDate = x.CreatedDate
            }).ToList()
        };
    }

    public async Task<EngineeringDocumentSummaryCountsDto> GetSummaryCountsAsync(bool restrictToDiscipline, int? disciplineId)
    {
        return await _repository.GetSummaryCountsAsync(restrictToDiscipline, disciplineId);
    }

    public async Task<List<EngineeringRevisionQualityDto>> GetRevisionQualityAsync(bool restrictToDiscipline, int? disciplineId, int? minRevisionCount, int? projectId = null)
    {
        return await _repository.GetRevisionQualityAsync(restrictToDiscipline, disciplineId, minRevisionCount, projectId);
    }



    public async Task<EngineeringDocumentDto?> GetByIdAsync(int id)
    {
        var document = await _repository.GetByIdAsync(id);


        if (document == null)
            return null;



        var checkedSignaturePath = await _repository.GetEmployeeSignaturePathAsync(document.CheckedById);
        var approvedSignaturePath = await _repository.GetEmployeeSignaturePathAsync(document.ApprovedById);

        return new EngineeringDocumentDto
        {
            DocumentId = document.DocumentId,


            // Basic Information

            DocumentNo = document.DocumentNo,

            Title = document.Title,

            Description = document.Description,

            DocumentType = document.DocumentType,


            ProjectId = document.ProjectId,

            ProjectName = document.Project != null
                ? document.Project.ProjectName
                : "",


            DisciplineId = document.DisciplineId ?? 0,

            DisciplineName = document.Discipline != null
                ? document.Discipline.Name
                : "",


            EquipmentId = document.EquipmentId,

            EquipmentTagNumber = document.Equipment != null ? document.Equipment.TagNumber : null,

            ContractId = document.ContractId,

            ContractNo = document.Contract != null ? document.Contract.ContractNo : null,



            Status = document.Status,

            RevisionNo = document.RevisionNo ?? "00",

            Area = document.Area,

            CommentStatus = document.CommentStatus,

            IsFrozen = document.IsFrozen,
            FrozenDate = document.FrozenDate,
            FrozenByName = document.FrozenByEmployee != null ? document.FrozenByEmployee.FirstName + " " + document.FrozenByEmployee.LastName : null,
            FreezeReason = document.FreezeReason,
            UnfrozenDate = document.UnfrozenDate,
            UnfrozenByName = document.UnfrozenByEmployee != null ? document.UnfrozenByEmployee.FirstName + " " + document.UnfrozenByEmployee.LastName : null,
            UnfreezeReason = document.UnfreezeReason,

            IsOnHold = document.IsOnHold,

            PlannedDate = document.PlannedDate,

            ActualDate = document.ActualDate,

            DelayDays = document.DelayDays,

            CreatedDate = document.CreatedDate,



            // Engineering Information

            IssuePurpose = document.IssuePurpose,

            PreparedBy = document.PreparedBy,

            CheckedBy = document.CheckedBy,

            CheckedBySignaturePath = checkedSignaturePath,

            ApprovedBy = document.ApprovedBy,

            ApprovedBySignaturePath = approvedSignaturePath,

            CheckedDate = document.CheckedDate,
            ApprovedDate = document.ApprovedDate,
            ClientApprovalStatus = document.ClientApprovalStatus,
            ClientComment = document.ClientComment,
            ClientResponseDate = document.ClientResponseDate,



            // Attachments

            Attachments = document.Attachments

                .Where(x => x.IsActive)

                .Select(x => new DocumentAttachmentDto
                {
                    AttachmentId = x.AttachmentId,

                    FileName = x.FileName,

                    FileExtension = x.FileExtension,

                    FilePath = x.FilePath,

                    FileSize = x.FileSize,

                    MimeType = x.MimeType,

                    DocumentRevisionId = x.DocumentRevisionId,

                    RevisionNo = x.DocumentRevisionId.HasValue
                        ? document.Revisions.FirstOrDefault(r => r.RevisionId == x.DocumentRevisionId.Value)?.RevisionNo
                        : null,

                    IsOfficial = x.IsOfficial,

                    FileRole = x.FileRole,

                    FileHash = x.FileHash,

                    AIStatus = x.AIStatus,

                    AISummary = x.AISummary,

                    AIKeywords = x.AIKeywords,

                    UploadDate = x.UploadDate

                })

                .ToList(),




            // Revision History

            Revisions = document.Revisions

                .Where(x => x.IsActive)

                .Select(x => new DocumentRevisionDto
                {
                    RevisionId = x.RevisionId,

                    RevisionNo = x.RevisionNo,

                    Status = x.Status,

                    Comment = x.Comment,

                    RevisionDate = x.RevisionDate,


                    PreparedBy = x.PreparedBy != null
                        ? x.PreparedBy.FirstName + " " + x.PreparedBy.LastName
                        : "",


                    CheckedBy = x.CheckedBy != null
                        ? x.CheckedBy.FirstName + " " + x.CheckedBy.LastName
                        : "",


                    ApprovedBy = x.ApprovedBy != null
                        ? x.ApprovedBy.FirstName + " " + x.ApprovedBy.LastName
                        : "",

                    Reason = x.Reason,
                    ChangeDescription = x.ChangeDescription,
                    VendorComment = x.VendorComment,
                    ReviewerName = x.Reviewer != null ? x.Reviewer.FirstName + " " + x.Reviewer.LastName : null,
                    CreatedByName = x.CreatedBy != null ? x.CreatedBy.FirstName + " " + x.CreatedBy.LastName : null,
                    CreatedDate = x.CreatedDate,
                    IssuedDate = x.IssuedDate,
                    EffectiveDate = x.EffectiveDate,
                    SupersededRevisionNo = x.SupersededRevision != null ? x.SupersededRevision.RevisionNo : null,
                    AIRevisionSummary = x.AIRevisionSummary,

                    ClientReviewStatus = x.ClientReviewStatus,
                    ClientReviewComment = x.ClientReviewComment,
                    ClientReviewDate = x.ClientReviewDate

                })

                .ToList(),





            // Workflow History

            Workflows = document.Workflows

    .Where(x => x.IsActive)

    .Select(x => new DocumentWorkflowDto
    {
        WorkflowId = x.WorkflowId,

        FromStatus = x.FromStatus,

        ToStatus = x.ToStatus,

        ActionType = x.ActionType,

        Comment = x.Comment,

        ActionDate = x.ActionDate,


        ActionBy = x.ActionBy != null
            ? x.ActionBy.FirstName + " " + x.ActionBy.LastName
            : ""

    })

    .ToList()

        };
    }

    // طبق ممیزی مقیاس‌پذیری (P1 - Document Register Remediation): Passthrough نازک - تمام
    // منطق واقعی (فیلتر/صفحه‌بندی/Projection/سقف امن) در Repository است، دقیقاً هم‌الگو با
    // GetPagedAsync موجود بالا.
    public Task<PagedResult<EngineeringDocumentDto>> GetRegisterPagedAsync(
        DocumentRegisterFilter filter, bool restrictToDiscipline, int? disciplineId)
        => _repository.GetRegisterPagedAsync(filter, restrictToDiscipline, disciplineId);

    public Task<(List<EngineeringDocumentDto> Items, int TotalMatching)> GetRegisterAllMatchingAsync(
        DocumentRegisterFilter filter, bool restrictToDiscipline, int? disciplineId, int maxRows)
        => _repository.GetRegisterAllMatchingAsync(filter, restrictToDiscipline, disciplineId, maxRows);

    public Task<List<EngineeringDocumentDto>> GetRegisterByIdsAsync(
        List<int> ids, bool restrictToDiscipline, int? disciplineId)
        => _repository.GetRegisterByIdsAsync(ids, restrictToDiscipline, disciplineId);

    public Task<DocumentRegisterFilterOptionsDto> GetRegisterFilterOptionsAsync(bool restrictToDiscipline, int? disciplineId)
        => _repository.GetRegisterFilterOptionsAsync(restrictToDiscipline, disciplineId);





    public async Task AddAsync(
        EngineeringDocument document)
    {
        await _repository.AddAsync(document);

        // طبق فاز D: قبلاً هیچ مدرک مهندسی‌ای برای Semantic Search Index نمی‌شد - فقط
        // پیوست‌ها (از طریق IntakeService). این‌جا خودِ محتوای اصلی مدرک هم اضافه می‌شود.
        // اگر Ollama در دسترس نباشد یا مدل Embedding نصب نباشد، IndexAsync خودش بی‌صدا رد
        // می‌شود - نباید ساخت مدرک را متوقف کند.
        await IndexForSemanticSearchAsync(document);
    }





    public async Task<EngineeringDocument?> GetEntityByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }





    public async Task UpdateAsync(
        EngineeringDocument document)
    {
        await _repository.UpdateAsync(document);

        await IndexForSemanticSearchAsync(document);
    }

    public async Task FreezeAsync(int documentId, int? actorEmployeeId, string reason)
    {
        await _repository.FreezeAsync(documentId, actorEmployeeId, reason);
    }

    public async Task UnfreezeAsync(int documentId, int? actorEmployeeId, string reason)
    {
        await _repository.UnfreezeAsync(documentId, actorEmployeeId, reason);
    }


    private async Task IndexForSemanticSearchAsync(EngineeringDocument document)
    {
        try
        {
            var content = string.Join(" ", new[] { document.DocumentNo, document.Title, document.Description }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            if (!string.IsNullOrWhiteSpace(content))
                await _knowledgeBaseService.IndexAsync("EngineeringDocument", document.DocumentId, content);
        }
        catch
        {
            // طبق همون اصل فاز A: خطای Indexing نباید عملیات اصلی (ساخت/ویرایش مدرک) را متوقف کند
        }
    }





    public async Task DeleteAsync(
        EngineeringDocument document)
    {
        await _repository.DeleteAsync(document);
    }

}