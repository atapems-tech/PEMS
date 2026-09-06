using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Exceptions;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using System.Linq.Expressions;

namespace PEMS.Persistence.Repositories;

public class EngineeringDocumentRepository
    : IEngineeringDocumentRepository
{
    private readonly PEMSDbContext _context;


    public EngineeringDocumentRepository(
        PEMSDbContext context)
    {
        _context = context;
    }



    public async Task<List<EngineeringDocument>> GetAllAsync()
    {
        return await _context.EngineeringDocuments
            .AsNoTracking()

            .Where(x => x.IsActive)

            .Include(x => x.Discipline)

            .Include(x => x.Project)

            .Include(x => x.Attachments)

            .Include(x => x.Equipment)

            .Include(x => x.Contract)

            .ToListAsync();
    }

    // طبق «PEMS — Global Grid Hardening» P1: پیوستگی با GetAllAsync بالا - فقط فیلترهای
    // Discipline.Name/DocumentType/Status (که AISearchController امروز فقط بعد از بارگذاری کل
    // جدول در حافظه اعمال می‌کرد) قبل از Include(Attachments) در SQL اعمال می‌شوند.
    public async Task<List<EngineeringDocument>> GetActiveForSearchAsync(string? disciplineName, string? documentType, string? status)
    {
        var query = _context.EngineeringDocuments
            .AsNoTracking()
            .Where(x => x.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(disciplineName))
            query = query.Where(x => x.Discipline != null && x.Discipline.Name.Contains(disciplineName));

        if (!string.IsNullOrWhiteSpace(documentType))
            query = query.Where(x => x.DocumentType.Contains(documentType));

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        return await query
            .Include(x => x.Discipline)
            .Include(x => x.Project)
            .Include(x => x.Attachments)
            .Include(x => x.Equipment)
            .Include(x => x.Contract)
            .ToListAsync();
    }



    // طبق Phase E-03 (رفع P1 ممیزی Performance): فقط Include هایی که Index.cshtml واقعاً
    // نمایش می‌دهد (Discipline/Project) - Attachments/Equipment/Contract فقط در Details لازم‌اند.
    public async Task<PagedResult<EngineeringDocument>> GetPagedAsync(
        EngineeringDocumentListFilter filter, bool restrictToDiscipline, int? disciplineId)
    {
        var query = _context.EngineeringDocuments
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Include(x => x.Discipline)
            .Include(x => x.Project)
            .AsQueryable();

        query = ApplyFilters(query, filter, restrictToDiscipline, disciplineId);

        var totalCount = await query.CountAsync();

        // طبق سند Global Data Grid Standard بند ۴/۴۳: قبلاً filter.PageSize بدون هیچ اعتبارسنجی
        // مستقیم در Take() استفاده می‌شد - یک مقدار دلخواه/منفی/صفر از QueryString می‌توانست
        // مستقیم به لایه‌ی SQL برسد. حالا فقط ۲۰/۵۰/۱۰۰ پذیرفته می‌شود.
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = DataGridPageSize.Normalize(filter.PageSize);

        var items = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.DocumentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<EngineeringDocument>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<EngineeringDocumentSummaryCountsDto> GetSummaryCountsAsync(bool restrictToDiscipline, int? disciplineId)
    {
        var query = _context.EngineeringDocuments.AsNoTracking().Where(x => x.IsActive).AsQueryable();

        if (restrictToDiscipline)
            query = query.Where(x => x.DisciplineId == disciplineId || x.DisciplineId == null);

        var now = DateTime.Now;

        var summary = await query
            .GroupBy(x => 1)
            .Select(g => new EngineeringDocumentSummaryCountsDto
            {
                Total = g.Count(),
                Approved = g.Sum(x => x.Status == "Approved" ? 1 : 0),
                Pending = g.Sum(x => x.Status == "Submitted" || x.Status == "Checked" ? 1 : 0),
                Rejected = g.Sum(x => x.Status == "Rejected" ? 1 : 0),
                Frozen = g.Sum(x => x.IsFrozen ? 1 : 0),
                Overdue = g.Sum(x =>
                    x.PlannedDate != null &&
                    ((x.ActualDate != null && x.ActualDate > x.PlannedDate) ||
                     (x.ActualDate == null && x.PlannedDate < now)) ? 1 : 0)
            })
            .FirstOrDefaultAsync();

        return summary ?? new EngineeringDocumentSummaryCountsDto();
    }

    // طبق Phase E-05C (E-05B §9، ADR-1): سه Query کوچک مستقل - نه یک Join غول‌پیکر روی کل
    // EngineeringDocument. هر سه محدودیت دیسیپلین را جدا اعمال می‌کنند (طبق §3 معماری E-05B،
    // این محدودیت باید روی هر Query اعمال شود، نه فقط لیست اصلی سند).
    public async Task<List<EngineeringRevisionQualityDto>> GetRevisionQualityAsync(
        bool restrictToDiscipline, int? disciplineId, int? minRevisionCount, int? projectId = null)
    {
        var docsQuery = _context.EngineeringDocuments
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Include(x => x.Discipline)
            .Include(x => x.Vendor)
            .AsQueryable();

        if (restrictToDiscipline)
            docsQuery = docsQuery.Where(x => x.DisciplineId == disciplineId || x.DisciplineId == null);

        // طبق E-05B §5 Filter Contract: Project یکی از ابعاد فیلتر Dashboard است - قبلاً این‌جا
        // جا افتاده بود، حین تست روی داده‌ی واقعی (هزاران سند موجود در دیتابیس dev) کشف شد.
        if (projectId.HasValue)
            docsQuery = docsQuery.Where(x => x.ProjectId == projectId.Value);

        var docs = await docsQuery
            .Select(x => new EngineeringRevisionQualityDto
            {
                DocumentId = x.DocumentId,
                DocumentNo = x.DocumentNo,
                Title = x.Title,
                DisciplineName = x.Discipline != null ? x.Discipline.Name : null,
                VendorName = x.Vendor != null ? x.Vendor.Name : null,
                Status = x.Status
            })
            .ToListAsync();

        if (docs.Count == 0)
            return docs;

        var docIds = docs.Select(d => d.DocumentId).ToList();

        // RevisionCount = COUNT(*) - 1 (رویژن ۰۰ همیشه ساخته می‌شود - طبق E-05A §3)
        var revisionCounts = await _context.DocumentRevisions
            .AsNoTracking()
            .Where(r => r.IsActive && docIds.Contains(r.DocumentId))
            .GroupBy(r => r.DocumentId)
            .Select(g => new { DocumentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DocumentId, x => Math.Max(0, x.Count - 1));

        // RejectionCount = تعداد تصمیم‌های Reject در تاریخچه‌ی همه‌ی Instance های این سند
        var rejectionCounts = await _context.WorkflowHistories
            .AsNoTracking()
            .Where(h => h.Decision == "Reject")
            .Join(
                _context.WorkflowInstances.Where(wi =>
                    wi.Module == "EngineeringDocument" && wi.BusinessObjectType == "Document" && docIds.Contains(wi.BusinessObjectId)),
                h => h.WorkflowInstanceId, wi => wi.WorkflowInstanceId,
                (h, wi) => wi.BusinessObjectId)
            .GroupBy(id => id)
            .Select(g => new { DocumentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DocumentId, x => x.Count);

        // FirstPassApproved = طبق Gate اعتبارسنجی‌شده‌ی E-05C (NOT EXISTS correlated subquery،
        // سمت سرور Translate می‌شود - PEMS.Tests/Engineering/FirstPassApprovalPrototypeTests):
        // زودترین Instance (کمترین StartDate) این سند Completed بوده یا نه.
        var instancesQuery = _context.WorkflowInstances
            .AsNoTracking()
            .Where(wi => wi.Module == "EngineeringDocument" && wi.BusinessObjectType == "Document" && docIds.Contains(wi.BusinessObjectId));

        var earliestInstanceStatus = await instancesQuery
            .Where(wi => !instancesQuery.Any(other =>
                other.BusinessObjectId == wi.BusinessObjectId && other.StartDate < wi.StartDate))
            .Select(wi => new { DocumentId = wi.BusinessObjectId, wi.Status })
            .ToDictionaryAsync(x => x.DocumentId, x => x.Status);

        foreach (var d in docs)
        {
            d.RevisionCount = revisionCounts.GetValueOrDefault(d.DocumentId, 0);
            d.RejectionCount = rejectionCounts.GetValueOrDefault(d.DocumentId, 0);
            d.FirstPassApproved = earliestInstanceStatus.TryGetValue(d.DocumentId, out var status)
                ? status == "Completed"
                : (bool?)null;
        }

        return minRevisionCount.HasValue
            ? docs.Where(d => d.RevisionCount >= minRevisionCount.Value).ToList()
            : docs;
    }

    private IQueryable<EngineeringDocument> ApplyFilters(
        IQueryable<EngineeringDocument> query, EngineeringDocumentListFilter filter, bool restrictToDiscipline, int? disciplineId)
    {
        if (restrictToDiscipline)
            query = query.Where(x => x.DisciplineId == disciplineId || x.DisciplineId == null);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x => x.DocumentNo.Contains(term) || x.Title.Contains(term));
        }

        if (filter.ProjectId.HasValue)
            query = query.Where(x => x.ProjectId == filter.ProjectId.Value);

        if (filter.DisciplineId.HasValue)
            query = query.Where(x => x.DisciplineId == filter.DisciplineId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(x => x.Status == filter.Status);

        if (!string.IsNullOrWhiteSpace(filter.Area))
            query = query.Where(x => x.Area != null && x.Area.Contains(filter.Area));

        // طبق Phase E-05C (Drilldown "High Revision Risk" - ADR-5): همون منطق COUNT-1 که
        // GetRevisionQualityAsync استفاده می‌کند، این‌جا به‌صورت Subquery همبسته
        if (filter.MinRevisionCount.HasValue)
        {
            var threshold = filter.MinRevisionCount.Value;
            query = query.Where(x =>
                _context.DocumentRevisions.Count(r => r.DocumentId == x.DocumentId && r.IsActive) - 1 >= threshold);
        }

        return query;
    }




    // طبق ممیزی مقیاس‌پذیری (P1 - Document Detail Query): بازرسی SQL واقعی (ToQueryString) این
    // Query ثابت کرد سه Collection خواهر-برادر (Attachments مستقیم + Revisions به‌صورت Subquery
    // + Workflows به‌صورت Subquery) روی همون ردیف والد Join می‌شوند - یعنی SQL Server واقعاً
    // ضرب دکارتی برمی‌گرداند (Attachments × Revisions × Workflows)، نه جمع. با داده‌ی واقعی
    // PEMSDB: بدترین سند موجود (DocumentId=24382) دقیقاً ۲۶ Revision × ۷۶ Workflow = ۱۹۷۶ ردیف
    // خام برای یک سند برمی‌گرداند (به‌جای ۱۰۳ شیء واقعی) - میانگین سیستم ≈ ۲٫۷ Revision × ۸٫۱
    // Workflow ≈ ۱٫۸ برابر اضافه‌بار. AsSplitQuery (راهکار رسمی مستندات EF Core برای دقیقاً همین
    // الگو - چند Include از نوع Collection روی یک ریشه) این ضرب را با N پرس‌وجوی SQL جدا (هر
    // Collection جدا) به جمع تبدیل می‌کند - شکل شیء برگشتی (EngineeringDocument کامل، برای
    // مصرف‌کننده‌های ویرایش/تایید) دست‌نخورده می‌ماند، فقط راهبرد اجرای SQL تغییر می‌کند.
    public async Task<EngineeringDocument?> GetByIdAsync(int id)
    {
        return await _context.EngineeringDocuments
            .AsSplitQuery()

            .Where(x => x.IsActive)

            .Include(x => x.Discipline)

            .Include(x => x.Project)

            .Include(x => x.Attachments)

            .Include(x => x.Equipment)

            .Include(x => x.Contract)

            .Include(x => x.FrozenByEmployee)

            .Include(x => x.UnfrozenByEmployee)


            .Include(x => x.Revisions)
                .ThenInclude(x => x.PreparedBy)

            .Include(x => x.Revisions)
                .ThenInclude(x => x.CheckedBy)

            .Include(x => x.Revisions)
                .ThenInclude(x => x.ApprovedBy)

            .Include(x => x.Revisions)
                .ThenInclude(x => x.Reviewer)

            .Include(x => x.Revisions)
                .ThenInclude(x => x.CreatedBy)

            .Include(x => x.Revisions)
                .ThenInclude(x => x.SupersededRevision)


            .Include(x => x.Workflows)
                .ThenInclude(x => x.ActionBy)


            .FirstOrDefaultAsync(
                x => x.DocumentId == id);
    }


    // طبق ممیزی مقیاس‌پذیری (P1 - Document Register Remediation): جایگزین GetAllAsync برای
    // DocumentRegisterController. عمداً بدون هیچ Include - همه‌ی فیلدهایی که View واقعاً نمایش
    // می‌دهد (DocumentNo/Title/Type/Area/DisciplineName/ProjectName/RevisionNo/Status/
    // CommentStatus/IsFrozen/DelayDays/CreatedDate) مستقیم در Select پروجکت می‌شوند - نه
    // Attachments/Revisions/Workflows/PreparedBy/CheckedBy/ApprovedBy که این صفحه هرگز نمایش
    // نمی‌دهد ولی GetAllAsync قبلی (با Include(Attachments)) بی‌فایده بارگذاری می‌کرد.
    //
    // DelayDays روی Document.cs یک Property محاسبه‌شده‌ی [NotMapped] است (شامل مقایسه با
    // DateTime.Now و شاخه‌بندی) - EF Core نمی‌تواند منطق Getter را مستقیم به SQL ترجمه کند.
    // به‌جای تغییر فرمول (که رفتار را می‌شکند)، PlannedDate/ActualDate در SQL پروجکت می‌شوند و
    // DelayDays بعد از واکشی (فقط روی ردیف‌های همون صفحه - نه کل جدول) با همون فرمول دقیق
    // Document.DelayDays محاسبه می‌شود.
    private IQueryable<EngineeringDocument> BuildRegisterQuery(
        DocumentRegisterFilter filter, bool restrictToDiscipline, int? disciplineId)
    {
        var query = _context.EngineeringDocuments
            .AsNoTracking()
            .Where(x => x.IsActive)
            .AsQueryable();

        if (restrictToDiscipline)
            query = query.Where(x => x.DisciplineId == disciplineId || x.DisciplineId == null);

        if (!string.IsNullOrWhiteSpace(filter.ProjectName))
            query = query.Where(x => x.Project != null && x.Project.ProjectName == filter.ProjectName);

        if (filter.Disciplines.Count > 0)
            query = query.Where(x =>
                filter.Disciplines.Contains(x.Discipline != null ? x.Discipline.Name : DocumentRegisterFilter.UnassignedDisciplineKey));

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(x => x.Status == filter.Status);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x => x.DocumentNo.Contains(term) || x.Title.Contains(term));
        }

        return query;
    }

    private static readonly Expression<Func<EngineeringDocument, EngineeringDocumentDto>> RegisterProjection = x => new EngineeringDocumentDto
    {
        DocumentId = x.DocumentId,
        DocumentNo = x.DocumentNo,
        Title = x.Title,
        DocumentType = x.DocumentType,
        ProjectId = x.ProjectId,
        ProjectName = x.Project != null ? x.Project.ProjectName : "",
        DisciplineId = x.DisciplineId ?? 0,
        DisciplineName = x.Discipline != null ? x.Discipline.Name : "",
        Status = x.Status,
        RevisionNo = x.RevisionNo ?? "00",
        Area = x.Area,
        CommentStatus = x.CommentStatus,
        IsFrozen = x.IsFrozen,
        PlannedDate = x.PlannedDate,
        ActualDate = x.ActualDate,
        CreatedDate = x.CreatedDate
    };

    // دقیقاً همون فرمول Document.DelayDays (PEMS.Domain/Entities/Document.cs) - عمداً کپی
    // نه فراخوانی، چون آن Property [NotMapped] است و روی یک شیء جداشده (DTO) قابل صدازدن نیست
    private static int? ComputeDelayDays(DateTime? plannedDate, DateTime? actualDate)
    {
        if (plannedDate == null) return null;

        var reference = actualDate ?? (plannedDate < DateTime.Now ? DateTime.Now : (DateTime?)null);
        if (reference == null) return null;

        return (int)(reference.Value - plannedDate.Value).TotalDays;
    }

    private static void ApplyDelayDaysAndClearRawDates(List<EngineeringDocumentDto> items)
    {
        // خروجی نهایی دقیقاً هم‌شکل GetAllAsync قبلی می‌ماند (که PlannedDate/ActualDate را در
        // DTO ست نمی‌کرد، فقط DelayDays محاسبه‌شده را) - این دو فیلد فقط ابزار محاسبه بودند
        foreach (var item in items)
        {
            item.DelayDays = ComputeDelayDays(item.PlannedDate, item.ActualDate);
            item.PlannedDate = null;
            item.ActualDate = null;
        }
    }

    public async Task<PagedResult<EngineeringDocumentDto>> GetRegisterPagedAsync(
        DocumentRegisterFilter filter, bool restrictToDiscipline, int? disciplineId)
    {
        var query = BuildRegisterQuery(filter, restrictToDiscipline, disciplineId);

        var totalCount = await query.CountAsync();

        var page = filter.Page < 1 ? 1 : filter.Page;
        // طبق سند Global Data Grid Standard بند ۴۳: Pagination هرگز نباید فقط به Clamp سطح
        // Controller اعتماد کند - Repository هم دوباره (Defense-in-Depth) فقط ۲۰/۵۰/۱۰۰ را
        // می‌پذیرد، حتی اگر یک فراخوان داخلی دیگر در آینده این‌جا را مستقیم صدا بزند
        var pageSize = DataGridPageSize.Normalize(filter.PageSize);

        var items = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.DocumentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(RegisterProjection)
            .ToListAsync();

        ApplyDelayDaysAndClearRawDates(items);

        return new PagedResult<EngineeringDocumentDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<(List<EngineeringDocumentDto> Items, int TotalMatching)> GetRegisterAllMatchingAsync(
        DocumentRegisterFilter filter, bool restrictToDiscipline, int? disciplineId, int maxRows)
    {
        var query = BuildRegisterQuery(filter, restrictToDiscipline, disciplineId);

        var totalMatching = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.DocumentId)
            .Take(maxRows)
            .Select(RegisterProjection)
            .ToListAsync();

        ApplyDelayDaysAndClearRawDates(items);

        return (items, totalMatching);
    }

    public async Task<List<EngineeringDocumentDto>> GetRegisterByIdsAsync(
        List<int> ids, bool restrictToDiscipline, int? disciplineId)
    {
        if (ids.Count == 0) return new List<EngineeringDocumentDto>();

        var query = _context.EngineeringDocuments
            .AsNoTracking()
            .Where(x => x.IsActive && ids.Contains(x.DocumentId));

        if (restrictToDiscipline)
            query = query.Where(x => x.DisciplineId == disciplineId || x.DisciplineId == null);

        var items = await query
            .Select(RegisterProjection)
            .ToListAsync();

        ApplyDelayDaysAndClearRawDates(items);

        return items;
    }

    public async Task<DocumentRegisterFilterOptionsDto> GetRegisterFilterOptionsAsync(bool restrictToDiscipline, int? disciplineId)
    {
        var baseQuery = _context.EngineeringDocuments.AsNoTracking().Where(x => x.IsActive).AsQueryable();

        if (restrictToDiscipline)
            baseQuery = baseQuery.Where(x => x.DisciplineId == disciplineId || x.DisciplineId == null);

        var projects = await baseQuery
            .Where(x => x.Project != null)
            .Select(x => x.Project!.ProjectName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        var disciplines = await baseQuery
            .Select(x => x.Discipline != null ? x.Discipline.Name : DocumentRegisterFilter.UnassignedDisciplineKey)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        var statuses = await baseQuery
            .Select(x => x.Status)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return new DocumentRegisterFilterOptionsDto
        {
            Projects = projects,
            Disciplines = disciplines,
            Statuses = statuses
        };
    }





    public async Task AddAsync(
        EngineeringDocument document)
    {
        await _context.EngineeringDocuments.AddAsync(document);

        await _context.SaveChangesAsync();
    }





    public async Task UpdateAsync(
        EngineeringDocument document)
    {
        // طبق رفع باگ: قبلاً شیء جداشده (Detached) از فرم مستقیم Update() می‌شد؛ چون RowVersion
        // (Concurrency Token) داخل فرم HTML رفت‌وبرگشت نمی‌کند، مقدارش همیشه خالی می‌رسید و
        // EF Core فکر می‌کرد رکورد توسط کاربر دیگری تغییر کرده (DbUpdateConcurrencyException).
        // راه‌حل درست: رکورد واقعی و ردیابی‌شده را از دیتابیس می‌خوانیم و فقط فیلدهای قابل‌ویرایش
        // را رویش کپی می‌کنیم - RowVersion واقعی همراه همان رکورد ردیابی‌شده باقی می‌ماند.
        var existing = await _context.EngineeringDocuments
            .FirstOrDefaultAsync(x => x.DocumentId == document.DocumentId);

        if (existing == null)
            throw new InvalidOperationException("مدرک برای ویرایش یافت نشد.");

        // طبق Phase E-01 (رفع P0 ممیزی Freeze): این‌جا نقطه‌ی واقعی SaveChanges برای ویرایش
        // مدرک است - چک این‌جا یعنی هیچ درخواست HTTP مستقیمی (نه فقط پنهان‌کردن دکمه در View)
        // نمی‌تواند این محدودیت را دور بزند.
        if (existing.IsFrozen)
            throw new DocumentFrozenException($"مدرک {existing.DocumentNo} منجمد (Frozen) است و قابل ویرایش نیست.");

        existing.DocumentNo = document.DocumentNo;
        existing.Title = document.Title;
        existing.Description = document.Description;
        existing.DocumentType = document.DocumentType;
        existing.ProjectId = document.ProjectId;
        existing.DisciplineId = document.DisciplineId;
        existing.Area = document.Area;
        existing.IssuePurpose = document.IssuePurpose;
        existing.PreparedBy = document.PreparedBy;
        existing.CheckedBy = document.CheckedBy;
        existing.ApprovedBy = document.ApprovedBy;
        existing.EquipmentId = document.EquipmentId;
        existing.ContractId = document.ContractId;
        existing.RelatedMRId = document.RelatedMRId;

        existing.ModifiedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }





    public async Task DeleteAsync(
        EngineeringDocument document)
    {
        document.IsActive = false;

        document.Status = "Archived";

        document.DeletedDate = DateTime.Now;


        await _context.SaveChangesAsync();
    }


    public async Task<string?> GetEmployeeSignaturePathAsync(int? employeeId)
    {
        if (!employeeId.HasValue) return null;

        return await _context.Employees
            .Where(e => e.EmployeeId == employeeId.Value)
            .Select(e => e.SignaturePath)
            .FirstOrDefaultAsync();
    }

    public async Task FreezeAsync(int documentId, int? actorEmployeeId, string reason)
    {
        var existing = await _context.EngineeringDocuments
            .FirstOrDefaultAsync(x => x.DocumentId == documentId);

        if (existing == null)
            throw new InvalidOperationException("مدرک یافت نشد.");

        if (existing.IsFrozen)
            throw new InvalidOperationException($"مدرک {existing.DocumentNo} از قبل منجمد است.");

        existing.IsFrozen = true;
        existing.FrozenDate = DateTime.Now;
        existing.FrozenById = actorEmployeeId;
        existing.FreezeReason = reason;
        existing.ModifiedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    public async Task UnfreezeAsync(int documentId, int? actorEmployeeId, string reason)
    {
        var existing = await _context.EngineeringDocuments
            .FirstOrDefaultAsync(x => x.DocumentId == documentId);

        if (existing == null)
            throw new InvalidOperationException("مدرک یافت نشد.");

        if (!existing.IsFrozen)
            throw new InvalidOperationException($"مدرک {existing.DocumentNo} منجمد نیست.");

        // طبق طراحی: تاریخچه‌ی انجماد قبلی (FrozenDate/FrozenById/FreezeReason) پاک نمی‌شود -
        // فقط فیلدهای Unfrozen* جدید ثبت می‌شوند تا سابقه‌ی کامل بماند.
        existing.IsFrozen = false;
        existing.UnfrozenDate = DateTime.Now;
        existing.UnfrozenById = actorEmployeeId;
        existing.UnfreezeReason = reason;
        existing.ModifiedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }
}