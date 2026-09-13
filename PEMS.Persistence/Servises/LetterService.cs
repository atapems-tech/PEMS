using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// پیاده‌سازی ماژول مکاتبات رسمی (Incoming/Outgoing/Internal یکپارچه با LetterType).
/// </summary>
public class LetterService : ILetterService
{
    private readonly PEMSDbContext _context;
    private readonly IRichTextSanitizerService _sanitizer;
    private readonly IKnowledgeBaseService _knowledgeBaseService;

    private static readonly string[] OutgoingStageOrder = { "Draft", "PendingApproval", "Approved", "Issued" };

    public LetterService(PEMSDbContext context, IRichTextSanitizerService sanitizer, IKnowledgeBaseService knowledgeBaseService)
    {
        _context = context;
        _sanitizer = sanitizer;
        _knowledgeBaseService = knowledgeBaseService;
    }

    public async Task<PagedResult<LetterDto>> SearchAsync(string? letterType, string? letterNumber, string? subject,
        string? sender, string? receiver, int? projectId, int? disciplineId, string? status,
        DateTime? fromDate, DateTime? toDate, int page, int pageSize,
        bool canViewConfidential, int? requestingApplicationUserId, bool starredOnly = false)
    {
        var query = _context.Letters
            .AsNoTracking()
            .Include(x => x.Project)
            .Include(x => x.Discipline)
            .Include(x => x.RelatedDocument)
            .Include(x => x.RelatedMR)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (starredOnly)
        {
            query = requestingApplicationUserId.HasValue
                ? query.Where(x => _context.LetterStars.Any(s => s.LetterId == x.LetterId && s.ApplicationUserId == requestingApplicationUserId.Value))
                : query.Where(x => false);
        }

        if (!string.IsNullOrWhiteSpace(letterType)) query = query.Where(x => x.LetterType == letterType);
        if (!string.IsNullOrWhiteSpace(letterNumber)) query = query.Where(x => x.LetterNumber.Contains(letterNumber));
        if (!string.IsNullOrWhiteSpace(subject)) query = query.Where(x => x.Subject.Contains(subject));
        if (!string.IsNullOrWhiteSpace(sender)) query = query.Where(x => x.Sender != null && x.Sender.Contains(sender));
        if (!string.IsNullOrWhiteSpace(receiver)) query = query.Where(x => x.Receiver != null && x.Receiver.Contains(receiver));
        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId);
        if (disciplineId.HasValue) query = query.Where(x => x.DisciplineId == disciplineId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (fromDate.HasValue) query = query.Where(x => x.LetterDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.LetterDate <= toDate.Value);

        // طبق گزارش ممیزی (بند ۶.۵): محدودیت محرمانگی باید در خودِ Query اعمال شود، نه فقط
        // در نمایش. کاربر بدون مجوز بالا فقط نامه‌های Public/Internal، یا نامه‌هایی که خودش
        // ثبت کرده، یا نامه‌هایی که به او ارجاع شده را می‌بیند.
        if (!canViewConfidential)
        {
            var employeeId = requestingApplicationUserId.HasValue
                ? await _context.Users.Where(x => x.Id == requestingApplicationUserId.Value)
                    .Select(x => x.EmployeeId).FirstOrDefaultAsync()
                : null;

            var referredLetterIds = employeeId.HasValue
                ? await _context.LetterReferrals.Where(x => x.ReferredToEmployeeId == employeeId.Value)
                    .Select(x => x.LetterId).ToListAsync()
                : new List<int>();

            query = query.Where(x =>
                x.ConfidentialityLevel == "Public" || x.ConfidentialityLevel == "Internal" ||
                (requestingApplicationUserId.HasValue && x.RegisteredById == requestingApplicationUserId.Value) ||
                referredLetterIds.Contains(x.LetterId));
        }

        var totalCount = await query.CountAsync();

        page = page < 1 ? 1 : page;
        // Defense-in-depth طبق سند Global Data Grid Standard - حتی اگر Controller قبلاً
        // Normalize کرده باشد، Service هم دوباره فقط ۲۰/۵۰/۱۰۰ را می‌پذیرد.
        pageSize = DataGridPageSize.Normalize(pageSize);

        var letters = await query
            .OrderByDescending(x => x.LetterDate)
            .ThenByDescending(x => x.LetterId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var letterIds = letters.Select(x => x.LetterId).ToList();

        var referralStats = await _context.LetterReferrals
            .Where(x => letterIds.Contains(x.LetterId))
            .ToListAsync();

        var starredIds = requestingApplicationUserId.HasValue
            ? await _context.LetterStars
                .Where(x => letterIds.Contains(x.LetterId) && x.ApplicationUserId == requestingApplicationUserId.Value)
                .Select(x => x.LetterId)
                .ToListAsync()
            : new List<int>();

        Dictionary<int, string> myRecipientTypes = new();

        if (requestingApplicationUserId.HasValue)
        {
            var myEmployeeId = await _context.Users
                .Where(x => x.Id == requestingApplicationUserId.Value)
                .Select(x => x.EmployeeId)
                .FirstOrDefaultAsync();

            if (myEmployeeId.HasValue)
            {
                myRecipientTypes = await _context.LetterRecipients
                    .Where(x => letterIds.Contains(x.LetterId) && x.EmployeeId == myEmployeeId.Value)
                    .GroupBy(x => x.LetterId)
                    // اگر هم To و هم Cc باشد، To اولویت دارد (گیرنده‌ی اصلی مهم‌تر است)
                    .ToDictionaryAsync(g => g.Key, g => g.Any(r => r.RecipientType == "To") ? "To" : "Cc");
            }
        }

        var letterTags = await _context.LetterTags
            .Include(x => x.Tag)
            .Where(x => letterIds.Contains(x.LetterId) && x.Tag != null && x.Tag.IsActive)
            .ToListAsync();

        // قبلاً یک Query جدا به‌ازای هر نامه اجرا می‌شد (N+1) - حالا یک Query گروهی واحد
        var attachmentCounts = await _context.DocumentAttachments
            .Where(x => x.LetterId.HasValue && letterIds.Contains(x.LetterId.Value))
            .GroupBy(x => x.LetterId!.Value)
            .Select(g => new { LetterId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LetterId, x => x.Count);

        var items = letters.Select(l => new LetterDto
        {
            LetterId = l.LetterId,
            LetterType = l.LetterType,
            LetterNumber = l.LetterNumber,
            LetterDate = l.LetterDate,
            Sender = l.Sender,
            Receiver = l.Receiver,
            Subject = l.Subject,
            Priority = l.Priority,
            ConfidentialityLevel = l.ConfidentialityLevel,
            ProjectName = l.Project?.ProjectName,
            DisciplineName = l.Discipline?.Name,
            RelatedDocumentId = l.RelatedDocumentId,
            RelatedDocumentNo = l.RelatedDocument?.DocumentNo,
            RelatedMRId = l.RelatedMRId,
            RelatedMRNumber = l.RelatedMR?.MRNumber,
            Status = l.Status,
            CreatedDate = l.CreatedDate,
            AttachmentsCount = attachmentCounts.TryGetValue(l.LetterId, out var ac) ? ac : 0,
            OpenReferralsCount = referralStats.Count(r => r.LetterId == l.LetterId && r.Status != "Completed"),
            HasOverdueReferral = referralStats.Any(r => r.LetterId == l.LetterId && r.Status != "Completed"
                && r.DueDate.HasValue && r.DueDate.Value < DateTime.Now),
            IsStarred = starredIds.Contains(l.LetterId),
            MyRecipientType = myRecipientTypes.TryGetValue(l.LetterId, out var rt) ? rt : null,
            Tags = letterTags.Where(lt => lt.LetterId == l.LetterId).Select(lt => new TagDto
            {
                TagId = lt.Tag!.TagId,
                Name = lt.Tag.Name,
                Color = lt.Tag.Color
            }).ToList()
        }).ToList();

        return new PagedResult<LetterDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<LetterDetailDto?> GetDetailAsync(int letterId)
    {
        var letter = await _context.Letters
            .Include(x => x.Project)
            .Include(x => x.Discipline)
            .Include(x => x.RelatedDocument)
            .Include(x => x.RelatedMR)
            .FirstOrDefaultAsync(x => x.LetterId == letterId);

        if (letter == null) return null;

        var referrals = await _context.LetterReferrals
            .Include(x => x.ReferredToEmployee)
            .Include(x => x.ReferredToDepartment)
            .Where(x => x.LetterId == letterId)
            .OrderByDescending(x => x.ReferredDate)
            .ToListAsync();

        var attachments = await _context.DocumentAttachments
            .Where(x => x.LetterId == letterId)
            .ToListAsync();

        var recipients = await _context.LetterRecipients
            .Include(x => x.Employee)
            .Include(x => x.Department)
            .Where(x => x.LetterId == letterId)
            .ToListAsync();

        var tags = await _context.LetterTags
            .Include(x => x.Tag)
            .Where(x => x.LetterId == letterId && x.Tag != null && x.Tag.IsActive)
            .Select(x => new TagDto { TagId = x.Tag!.TagId, Name = x.Tag.Name, Color = x.Tag.Color })
            .ToListAsync();

        var reviews = await _context.LetterReviews
            .Where(x => x.LetterId == letterId && x.IsActive)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        // طبق قرارداد موجود این Entity: RegisteredById/ApprovedById/ReturnedByUserId/
        // RejectedByUserId/ReviewerUserId همه ApplicationUserId هستند، بدون Navigation - نام
        // نمایشی این‌جا با یک Query گروهی واحد از روی ApplicationUsers.FullName حل می‌شود
        var actorUserIds = new[] { letter.ApprovedById, letter.ReturnedByUserId, letter.RejectedByUserId }
            .Concat(reviews.Select(r => r.ReviewerUserId))
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();

        var actorNames = actorUserIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.Users.Where(u => actorUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName ?? u.UserName ?? "-");

        string? NameOf(int? userId) => userId.HasValue && actorNames.TryGetValue(userId.Value, out var n) ? n : null;

        return new LetterDetailDto
        {
            Letter = new LetterDto
            {
                LetterId = letter.LetterId,
                LetterType = letter.LetterType,
                LetterNumber = letter.LetterNumber,
                LetterDate = letter.LetterDate,
                Sender = letter.Sender,
                Receiver = letter.Receiver,
                Subject = letter.Subject,
                Priority = letter.Priority,
                ConfidentialityLevel = letter.ConfidentialityLevel,
                ProjectName = letter.Project?.ProjectName,
                DisciplineName = letter.Discipline?.Name,
                RelatedDocumentId = letter.RelatedDocumentId,
                RelatedDocumentNo = letter.RelatedDocument?.DocumentNo,
                RelatedMRId = letter.RelatedMRId,
                RelatedMRNumber = letter.RelatedMR?.MRNumber,
                Status = letter.Status,
                CreatedDate = letter.CreatedDate,
                AttachmentsCount = attachments.Count,
                OpenReferralsCount = referrals.Count(r => r.Status != "Completed"),
                Tags = tags,
                SignedByName = letter.SignedByName,
                SignedDate = letter.SignedDate,
                RowVersion = letter.RowVersion,
                RegisteredById = letter.RegisteredById,
                ReturnedByName = NameOf(letter.ReturnedByUserId),
                ReturnedDate = letter.ReturnedDate,
                ReturnReason = letter.ReturnReason,
                RejectedByName = NameOf(letter.RejectedByUserId),
                RejectedDate = letter.RejectedDate,
                RejectReason = letter.RejectReason,
                ApprovedByName = NameOf(letter.ApprovedById),
                ApprovedDate = letter.ApprovedDate,
                IssuedDate = letter.IssuedDate,
                ClosedDate = letter.ClosedDate,
                RevisionCount = letter.RevisionCount
            },
            Body = letter.Body,
            Reviews = reviews.Select(r => new LetterReviewDto
            {
                LetterReviewId = r.LetterReviewId,
                ReviewerName = NameOf(r.ReviewerUserId),
                Decision = r.Decision,
                Comment = r.Comment,
                CreatedDate = r.CreatedDate,
                PreviousSubjectSnapshot = r.PreviousSubjectSnapshot,
                PreviousBodySnapshot = r.PreviousBodySnapshot
            }).ToList(),
            Referrals = referrals.Select(r => new LetterReferralDto
            {
                LetterReferralId = r.LetterReferralId,
                LetterId = r.LetterId,
                ReferredToEmployeeName = r.ReferredToEmployee != null
                    ? r.ReferredToEmployee.FirstName + " " + r.ReferredToEmployee.LastName
                    : null,
                ReferredToDepartmentName = r.ReferredToDepartment?.Name,
                Instructions = r.Instructions,
                DueDate = r.DueDate,
                Status = r.Status,
                IsRead = r.IsRead,
                ReadDate = r.ReadDate,
                ReferredByName = r.ReferredByName,
                ReferredDate = r.ReferredDate,
                ResponseNote = r.ResponseNote,
                IsOverdue = r.Status != "Completed" && r.DueDate.HasValue && r.DueDate.Value < DateTime.Now
            }).ToList(),
            Attachments = attachments.Select(a => new LetterAttachmentDto
            {
                AttachmentId = a.AttachmentId,
                FileName = a.FileName,
                FilePath = a.FilePath
            }).ToList(),
            Recipients = recipients.Select(r => new LetterRecipientDto
            {
                LetterRecipientId = r.LetterRecipientId,
                RecipientType = r.RecipientType,
                DisplayName = r.Employee != null
                    ? r.Employee.FirstName + " " + r.Employee.LastName
                    : r.Department?.Name ?? r.ExternalName ?? "-",
                IsRead = r.IsRead,
                ReadDate = r.ReadDate
            }).ToList()
        };
    }

    public async Task<int> CreateAsync(string letterType, string letterNumber, DateTime letterDate, string? sender,
        string? receiver, string subject, string? body, string priority, string confidentialityLevel,
        int? projectId, int? disciplineId, int? relatedDocumentId, int? relatedMRId, int? registeredById)
    {
        var letter = new Letter
        {
            LetterType = letterType,
            LetterNumber = letterNumber,
            LetterDate = letterDate,
            Sender = sender,
            Receiver = receiver,
            Subject = subject,
            Body = _sanitizer.Sanitize(body),
            Priority = string.IsNullOrWhiteSpace(priority) ? "Normal" : priority,
            ConfidentialityLevel = string.IsNullOrWhiteSpace(confidentialityLevel) ? "Internal" : confidentialityLevel,
            ProjectId = projectId,
            DisciplineId = disciplineId,
            RelatedDocumentId = relatedDocumentId,
            RelatedMRId = relatedMRId,
            RegisteredById = registeredById,
            Status = letterType == "Outgoing" ? "Draft" : letterType == "Incoming" ? "Registered" : "Sent",
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.Letters.AddAsync(letter);
        await _context.SaveChangesAsync();

        // طبق فاز D: قبلاً هیچ نامه‌ای برای Semantic Search Index نمی‌شد
        await IndexLetterForSemanticSearchAsync(letter);

        return letter.LetterId;
    }

    private async Task IndexLetterForSemanticSearchAsync(Letter letter)
    {
        try
        {
            var content = string.Join(" ", new[] { letter.LetterNumber, letter.Subject, StripHtml(letter.Body) }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            if (!string.IsNullOrWhiteSpace(content))
                await _knowledgeBaseService.IndexAsync("Letter", letter.LetterId, content);
        }
        catch
        {
            // خطای Indexing نباید ثبت نامه را متوقف کند
        }
    }

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ");
    }

    public async Task<List<RelatedLetterDto>> GetRelatedToDocumentAsync(int documentId)
    {
        return await _context.Letters
            .Where(x => x.IsActive && x.RelatedDocumentId == documentId)
            .OrderByDescending(x => x.LetterDate)
            .Select(x => new RelatedLetterDto
            {
                LetterId = x.LetterId,
                LetterType = x.LetterType,
                LetterNumber = x.LetterNumber,
                Subject = x.Subject,
                LetterDate = x.LetterDate,
                Status = x.Status
            })
            .ToListAsync();
    }

    public async Task<List<RelatedLetterDto>> GetRelatedToMRAsync(int mrId)
    {
        return await _context.Letters
            .Where(x => x.IsActive && x.RelatedMRId == mrId)
            .OrderByDescending(x => x.LetterDate)
            .Select(x => new RelatedLetterDto
            {
                LetterId = x.LetterId,
                LetterType = x.LetterType,
                LetterNumber = x.LetterNumber,
                Subject = x.Subject,
                LetterDate = x.LetterDate,
                Status = x.Status
            })
            .ToListAsync();
    }

    // ==============================================================================
    // طبق «PEMS Letter Details — Controlled Lifecycle» بند ۳۴: نگاشت گذارهای قانونی - کاربر
    // هرگز وضعیت دلخواه انتخاب نمی‌کند، فقط یک اقدام کسب‌وکاری صریح که خودش گذار درست را
    // اعمال می‌کند. این نگاشت فقط برای نامه‌های Outgoing/Internal است که واقعاً از این
    // گردش‌کار عبور می‌کنند - Incoming خط‌سیر ساده‌ی خودش (Registered/UnderReview/Referred/
    // Closed) را دارد و دست‌نخورده می‌ماند (بند ۳: هرگز به یک گردش‌کار یکسان اجباری نمی‌شود).
    // ==============================================================================
    private static readonly Dictionary<string, string> RequiredFromStatus = new()
    {
        [nameof(SubmitForReviewAsync)] = "Draft",
        [nameof(ApproveAsync)] = "PendingApproval",
        [nameof(ReturnForRevisionAsync)] = "PendingApproval",
        [nameof(RejectAsync)] = "PendingApproval",
        [nameof(IssueAsync)] = "Approved",
        [nameof(CloseAsync)] = "Issued",
    };

    private async Task<(Letter? Letter, string? Error)> LoadForTransitionAsync(int letterId, string requiredFromStatus, string actionLabel)
    {
        var letter = await _context.Letters.FirstOrDefaultAsync(x => x.LetterId == letterId && x.IsActive);
        if (letter == null) return (null, "نامه یافت نشد.");

        if (letter.Status != requiredFromStatus)
            return (null, $"«{actionLabel}» فقط از وضعیت «{requiredFromStatus}» ممکن است - وضعیت فعلی این نامه «{letter.Status}» است.");

        return (letter, null);
    }

    // طبق بند ۱۳: «مالکیت» یکی از چک‌های الزامی برای هر عملیات مهم است - فقط نویسنده‌ی اصلی
    // (RegisteredById) می‌تواند پیش‌نویس خودش را برای بازبینی ارسال کند؛ در غیر این‌صورت هر
    // کاربر احرازهویت‌شده‌ای می‌توانست پیش‌نویس نامه‌ی شخص دیگری را زودتر از موعد ارسال کند.
    public async Task<(bool Success, string? Error)> SubmitForReviewAsync(int letterId, int? actorApplicationUserId)
    {
        var (letter, error) = await LoadForTransitionAsync(letterId, RequiredFromStatus[nameof(SubmitForReviewAsync)], "Submit for Review");
        if (letter == null) return (false, error);

        if (letter.RegisteredById != actorApplicationUserId)
            return (false, "فقط نویسنده‌ی اصلی این نامه مجاز به ارسال آن برای بازبینی است.");

        letter.Status = "PendingApproval";
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ApproveAsync(int letterId, int? reviewerApplicationUserId, string? comment)
    {
        var (letter, error) = await LoadForTransitionAsync(letterId, RequiredFromStatus[nameof(ApproveAsync)], "Approve");
        if (letter == null) return (false, error);

        letter.Status = "Approved";
        letter.ApprovedDate = DateTime.Now;
        letter.ApprovedById = reviewerApplicationUserId;

        await _context.LetterReviews.AddAsync(new LetterReview
        {
            LetterId = letterId,
            ReviewerUserId = reviewerApplicationUserId,
            Decision = LetterReviewDecisions.Approved,
            Comment = comment,
            CreatedDate = DateTime.Now,
            IsActive = true
        });

        return await SaveWithConcurrencyCheckAsync();
    }

    public async Task<(bool Success, string? Error)> ReturnForRevisionAsync(int letterId, int? reviewerApplicationUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return (false, "دلیل برگشت برای اصلاح (Return Reason) الزامی است.");

        var (letter, error) = await LoadForTransitionAsync(letterId, RequiredFromStatus[nameof(ReturnForRevisionAsync)], "Return for Revision");
        if (letter == null) return (false, error);

        // طبق بند ۱۱: Snapshot محتوایی که بازبین همین الان دیده - پیش از این‌که نویسنده آن را ویرایش کند
        await _context.LetterReviews.AddAsync(new LetterReview
        {
            LetterId = letterId,
            ReviewerUserId = reviewerApplicationUserId,
            Decision = LetterReviewDecisions.ReturnedForRevision,
            Comment = reason,
            PreviousSubjectSnapshot = letter.Subject,
            PreviousBodySnapshot = letter.Body,
            CreatedDate = DateTime.Now,
            IsActive = true
        });

        letter.Status = "ReturnedForRevision";
        letter.ReturnedByUserId = reviewerApplicationUserId;
        letter.ReturnedDate = DateTime.Now;
        letter.ReturnReason = reason;

        return await SaveWithConcurrencyCheckAsync();
    }

    public async Task<(bool Success, string? Error)> RejectAsync(int letterId, int? reviewerApplicationUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return (false, "دلیل رد (Reject Reason) الزامی است.");

        var (letter, error) = await LoadForTransitionAsync(letterId, RequiredFromStatus[nameof(RejectAsync)], "Reject");
        if (letter == null) return (false, error);

        letter.Status = "Rejected";
        letter.RejectedByUserId = reviewerApplicationUserId;
        letter.RejectedDate = DateTime.Now;
        letter.RejectReason = reason;

        await _context.LetterReviews.AddAsync(new LetterReview
        {
            LetterId = letterId,
            ReviewerUserId = reviewerApplicationUserId,
            Decision = LetterReviewDecisions.Rejected,
            Comment = reason,
            CreatedDate = DateTime.Now,
            IsActive = true
        });

        return await SaveWithConcurrencyCheckAsync();
    }

    public async Task<(bool Success, string? Error)> IssueAsync(int letterId, int? actorApplicationUserId)
    {
        var (letter, error) = await LoadForTransitionAsync(letterId, RequiredFromStatus[nameof(IssueAsync)], "Issue");
        if (letter == null) return (false, error);

        letter.Status = "Issued";
        letter.IssuedDate = DateTime.Now;

        return await SaveWithConcurrencyCheckAsync();
    }

    public async Task<(bool Success, string? Error)> CloseAsync(int letterId, int? actorApplicationUserId)
    {
        var letter = await _context.Letters.FirstOrDefaultAsync(x => x.LetterId == letterId && x.IsActive);
        if (letter == null) return (false, "نامه یافت نشد.");

        // طبق بند ۳: Incoming/Internal خط‌سیر پایانی خودشان را دارند (Referred/Sent/Acknowledged
        // هم می‌توانند به Closed برسند) - فقط Outgoing باید حتماً از Issued عبور کرده باشد
        var validFromStatuses = letter.LetterType == "Outgoing"
            ? new[] { "Issued" }
            : new[] { "Registered", "UnderReview", "Referred", "Sent", "Acknowledged" };

        if (!validFromStatuses.Contains(letter.Status))
            return (false, $"«Close» از وضعیت فعلی «{letter.Status}» برای این نوع نامه ممکن نیست.");

        letter.Status = "Closed";
        letter.ClosedDate = DateTime.Now;

        return await SaveWithConcurrencyCheckAsync();
    }

    // ==============================================================================
    // طبق بند ۷: قلب کل قانون خودتاییدی این نشست - فقط CurrentUserId == RegisteredById AND
    // Status == ReturnedForRevision. نه بر اساس Role، نه بر اساس نام Employee، نه فقط مخفی‌کردن
    // دکمه در UI - این‌جا (لایه‌ی سرویس) تنها مرز واقعی اجراست.
    // ==============================================================================
    private static (bool Allowed, string? Error) CheckOriginalAuthorCanEdit(Letter letter, int currentApplicationUserId)
    {
        if (letter.RegisteredById != currentApplicationUserId)
            return (false, "فقط نویسنده‌ی اصلی این نامه مجاز به ویرایش محتوای آن است.");

        if (letter.Status != "Draft" && letter.Status != "ReturnedForRevision")
            return (false, $"محتوای رسمی این نامه در وضعیت «{letter.Status}» غیرقابل‌ویرایش است.");

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SaveDraftEditAsync(int letterId, string subject, string body,
        int currentApplicationUserId, byte[]? rowVersion)
    {
        var letter = await _context.Letters.FirstOrDefaultAsync(x => x.LetterId == letterId && x.IsActive);
        if (letter == null) return (false, "نامه یافت نشد.");

        var (allowed, error) = CheckOriginalAuthorCanEdit(letter, currentApplicationUserId);
        if (!allowed) return (false, error);

        ApplyConcurrencyToken(letter, rowVersion);

        letter.Subject = subject;
        letter.Body = _sanitizer.Sanitize(body);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "این نامه هم‌زمان توسط کاربر دیگری تغییر کرده است. لطفاً صفحه را رفرش کنید و دوباره تلاش کنید.");
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ResubmitAsync(int letterId, string subject, string body,
        int currentApplicationUserId, byte[]? rowVersion)
    {
        var letter = await _context.Letters.FirstOrDefaultAsync(x => x.LetterId == letterId && x.IsActive);
        if (letter == null) return (false, "نامه یافت نشد.");

        // طبق بند ۷/۳۵: بازارسال فقط از ReturnedForRevision و فقط توسط نویسنده‌ی اصلی
        if (letter.Status != "ReturnedForRevision")
            return (false, $"«Resubmit» فقط از وضعیت «ReturnedForRevision» ممکن است - وضعیت فعلی «{letter.Status}» است.");

        if (letter.RegisteredById != currentApplicationUserId)
            return (false, "فقط نویسنده‌ی اصلی این نامه مجاز به ارسال مجدد آن است.");

        ApplyConcurrencyToken(letter, rowVersion);

        letter.Subject = subject;
        letter.Body = _sanitizer.Sanitize(body);
        letter.RevisionCount += 1;

        // طبق بند ۳۵: بلافاصله دوباره غیرقابل‌ویرایش می‌شود - Cache برگشت پاک می‌شود (تاریخچه‌ی
        // واقعی در LetterReview دست‌نخورده می‌ماند، دقیقاً همون الگوی ClientReviewStatus فاز ۲ EDMS)
        letter.Status = "PendingApproval";
        letter.ReturnedByUserId = null;
        letter.ReturnedDate = null;
        letter.ReturnReason = null;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "این نامه هم‌زمان توسط کاربر دیگری تغییر کرده است. لطفاً صفحه را رفرش کنید و دوباره تلاش کنید.");
        }

        return (true, null);
    }

    // طبق بند ۲۹ (Concurrency): مقدار RowVersion ارسالی از فرم (که کاربر لحظه‌ی بازکردن صفحه
    // دیده) به‌عنوان OriginalValue تنظیم می‌شود - نه CurrentValue. این‌طوری دستور UPDATE واقعی
    // EF دقیقاً همان چیزی را در WHERE می‌گذارد که کاربر دیده؛ اگر بین‌این‌مدت کس دیگری ذخیره
    // کرده باشد، صفر ردیف تطبیق می‌یابد و DbUpdateConcurrencyException پرتاب می‌شود - برخلاف
    // اشتباه رایج «ست‌کردن مستقیم CurrentValue» که اصلاً هیچ تعارضی را تشخیص نمی‌دهد چون همین
    // متد لحظه‌ای قبلش خودش رکورد را تازه خوانده است.
    private void ApplyConcurrencyToken(Letter letter, byte[]? rowVersion)
    {
        if (rowVersion != null)
            _context.Entry(letter).Property(x => x.RowVersion).OriginalValue = rowVersion;
    }

    // طبق «PEMS Correspondence Workspace» بند ۳۰: تست Concurrency واقعی (روی SQL Server ایزوله،
    // نه InMemory) ثابت کرد این متد قبلاً یک Exception خام پرتاب می‌کرد به‌جای برگرداندن همون
    // Tuple (Success, Error) که بقیه‌ی متدهای این کلاس استفاده می‌کنند - یعنی وقتی دو درخواست
    // هم‌زمان (مثلاً Approve و ReturnForRevision) روی یک نامه هم‌پوشانی می‌کردند، بازنده به‌جای
    // یک پیام کنترل‌شده در TempData["Error"]، باعث یک خطای ۵۰۰ کنترل‌نشده در Controller می‌شد
    // (چون LetterController فقط منتظر Tuple است، نه try/catch اطراف هر Action). مکانیزم واقعی
    // Concurrency (RowVersion) درست کار می‌کرد - فقط شکل بازگشت خطا اشتباه بود.
    private async Task<(bool Success, string? Error)> SaveWithConcurrencyCheckAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
            return (true, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "این نامه هم‌زمان توسط کاربر دیگری تغییر کرده است. لطفاً صفحه را رفرش کنید و دوباره تلاش کنید.");
        }
    }

    public async Task<LetterDashboardDto> GetDashboardAsync(int? currentApplicationUserId = null)
    {
        var letters = await _context.Letters.Where(x => x.IsActive).ToListAsync();
        var referrals = await _context.LetterReferrals.ToListAsync();

        var dashboard = new LetterDashboardDto
        {
            PendingLetters = letters.Count(x => x.Status is "Registered" or "UnderReview" or "PendingApproval" or "Draft"),
            OverdueReferrals = referrals.Count(r => r.Status != "Completed" && r.DueDate.HasValue && r.DueDate.Value < DateTime.Now),
            RecentlyReceived = letters.Count(x => x.LetterType == "Incoming" && x.LetterDate >= DateTime.Now.AddDays(-7)),
            RecentlySent = letters.Count(x => x.LetterType == "Outgoing" && x.Status == "Issued" && x.IssuedDate >= DateTime.Now.AddDays(-7)),
            ByStatus = letters.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Count()),
            ByType = letters.GroupBy(x => x.LetterType).ToDictionary(g => g.Key, g => g.Count())
        };

        if (currentApplicationUserId.HasValue)
        {
            var employeeId = await _context.Users
                .Where(x => x.Id == currentApplicationUserId.Value)
                .Select(x => x.EmployeeId)
                .FirstOrDefaultAsync();

            if (employeeId.HasValue)
            {
                dashboard.MyOpenReferrals = referrals.Count(r =>
                    r.ReferredToEmployeeId == employeeId.Value && r.Status != "Completed");
            }
        }

        return dashboard;
    }

    public async Task ToggleStarAsync(int letterId, int applicationUserId)
    {
        var existing = await _context.LetterStars
            .FirstOrDefaultAsync(x => x.LetterId == letterId && x.ApplicationUserId == applicationUserId);

        if (existing != null)
        {
            _context.LetterStars.Remove(existing);
        }
        else
        {
            await _context.LetterStars.AddAsync(new LetterStar
            {
                LetterId = letterId,
                ApplicationUserId = applicationUserId,
                CreatedDate = DateTime.Now
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task AddRecipientAsync(int letterId, string recipientType, int? employeeId, int? departmentId, string? externalName)
    {
        await _context.LetterRecipients.AddAsync(new LetterRecipient
        {
            LetterId = letterId,
            RecipientType = string.IsNullOrWhiteSpace(recipientType) ? "To" : recipientType,
            EmployeeId = employeeId,
            DepartmentId = departmentId,
            ExternalName = externalName,
            IsRead = false,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
    }

    public async Task MarkRecipientReadAsync(int letterRecipientId, int applicationUserId)
    {
        var myEmployeeId = await _context.Users
            .Where(x => x.Id == applicationUserId)
            .Select(x => x.EmployeeId)
            .FirstOrDefaultAsync();

        var recipient = await _context.LetterRecipients
            .FirstOrDefaultAsync(x => x.LetterRecipientId == letterRecipientId
                && (!myEmployeeId.HasValue || x.EmployeeId == myEmployeeId.Value));

        if (recipient == null) return;

        recipient.IsRead = true;
        recipient.ReadDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    public async Task SignAsync(int letterId, string signedByName)
    {
        var letter = await _context.Letters.FirstOrDefaultAsync(x => x.LetterId == letterId);
        if (letter == null) return;

        letter.SignedByName = signedByName;
        letter.SignedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    public async Task<List<LetterTimelineEventDto>> GetTimelineAsync(int letterId)
    {
        var letter = await _context.Letters.FirstOrDefaultAsync(x => x.LetterId == letterId);
        if (letter == null) return new List<LetterTimelineEventDto>();

        var events = new List<LetterTimelineEventDto>
        {
            new()
            {
                EventDate = letter.CreatedDate,
                EventType = "Created",
                Description = $"{letter.LetterType} letter registered: {letter.Subject}",
                Icon = "bi-file-earmark-plus",
                Color = "primary"
            }
        };

        var recipients = await _context.LetterRecipients
            .Include(x => x.Employee)
            .Include(x => x.Department)
            .Where(x => x.LetterId == letterId)
            .ToListAsync();

        foreach (var r in recipients)
        {
            var name = r.Employee != null ? r.Employee.FirstName + " " + r.Employee.LastName : (r.Department?.Name ?? r.ExternalName ?? "-");

            events.Add(new LetterTimelineEventDto
            {
                EventDate = r.CreatedDate,
                EventType = "Recipient",
                Description = $"Added as {(r.RecipientType == "To" ? "recipient (To)" : "Cc")}: {name}",
                Icon = "bi-person-plus",
                Color = "secondary"
            });

            if (r.IsRead && r.ReadDate.HasValue)
            {
                events.Add(new LetterTimelineEventDto
                {
                    EventDate = r.ReadDate.Value,
                    EventType = "Read",
                    Description = $"Read by {name}",
                    Icon = "bi-check2-all",
                    Color = "success"
                });
            }
        }

        var referrals = await _context.LetterReferrals
            .Include(x => x.ReferredToEmployee)
            .Include(x => x.ReferredToDepartment)
            .Where(x => x.LetterId == letterId)
            .ToListAsync();

        foreach (var ref_ in referrals)
        {
            var target = ref_.ReferredToEmployee != null
                ? ref_.ReferredToEmployee.FirstName + " " + ref_.ReferredToEmployee.LastName
                : (ref_.ReferredToDepartment?.Name ?? "-");

            events.Add(new LetterTimelineEventDto
            {
                EventDate = ref_.ReferredDate,
                EventType = "Referred",
                Description = $"Referred to {target}" + (string.IsNullOrEmpty(ref_.Instructions) ? "" : $" — \"{ref_.Instructions}\""),
                ActorName = ref_.ReferredByName,
                Icon = "bi-signpost-split",
                Color = "warning"
            });

            if (ref_.IsRead && ref_.ReadDate.HasValue)
            {
                events.Add(new LetterTimelineEventDto
                {
                    EventDate = ref_.ReadDate.Value,
                    EventType = "Read",
                    Description = $"Referral read by {target}",
                    Icon = "bi-eye",
                    Color = "info"
                });
            }

            if (ref_.Status == "Completed" && ref_.CompletedDate.HasValue)
            {
                events.Add(new LetterTimelineEventDto
                {
                    EventDate = ref_.CompletedDate.Value,
                    EventType = "Completed",
                    Description = $"Referral completed by {target}" + (string.IsNullOrEmpty(ref_.ResponseNote) ? "" : $" — \"{ref_.ResponseNote}\""),
                    Icon = "bi-check-circle",
                    Color = "success"
                });
            }
        }

        if (letter.ApprovedDate.HasValue)
        {
            events.Add(new LetterTimelineEventDto
            {
                EventDate = letter.ApprovedDate.Value,
                EventType = "Status",
                Description = "Approved",
                Icon = "bi-check2-square",
                Color = "success"
            });
        }

        if (letter.IssuedDate.HasValue)
        {
            events.Add(new LetterTimelineEventDto
            {
                EventDate = letter.IssuedDate.Value,
                EventType = "Status",
                Description = "Issued",
                Icon = "bi-send-check",
                Color = "primary"
            });
        }

        if (letter.SignedDate.HasValue)
        {
            events.Add(new LetterTimelineEventDto
            {
                EventDate = letter.SignedDate.Value,
                EventType = "Signed",
                Description = $"Electronically acknowledged by {letter.SignedByName}",
                Icon = "bi-patch-check-fill",
                Color = "success"
            });
        }

        return events.OrderBy(x => x.EventDate).ToList();
    }

    public async Task<List<ThreadMessageDto>> GetThreadAsync(int letterId)
    {
        var chain = new List<ThreadMessageDto>();
        var currentId = (int?)letterId;
        var visited = new HashSet<int>();

        while (currentId.HasValue && visited.Add(currentId.Value))
        {
            var letter = await _context.Letters.FirstOrDefaultAsync(x => x.LetterId == currentId.Value);
            if (letter == null) break;

            var recipients = await _context.LetterRecipients
                .Include(x => x.Employee)
                .Include(x => x.Department)
                .Where(x => x.LetterId == letter.LetterId)
                .ToListAsync();

            var recipientsSummary = string.Join("، ", recipients.Select(r =>
                r.Employee != null ? r.Employee.FirstName + " " + r.Employee.LastName : (r.Department?.Name ?? r.ExternalName ?? "-")));

            chain.Add(new ThreadMessageDto
            {
                LetterId = letter.LetterId,
                Subject = letter.Subject,
                Body = letter.Body,
                LetterNumber = letter.LetterNumber,
                LetterDate = letter.LetterDate,
                SenderOrRegisteredByName = letter.LetterType == "Incoming" ? letter.Sender : letter.Receiver,
                RecipientsSummary = recipientsSummary
            });

            currentId = letter.ParentLetterId;
        }

        chain.Reverse();

        return chain;
    }

    public async Task<int> ReplyAsync(int parentLetterId, string letterNumber, string subject, string? body,
        string? receiver, int? registeredById)
    {
        var parent = await _context.Letters.FirstOrDefaultAsync(x => x.LetterId == parentLetterId);
        if (parent == null) throw new Exception("نامه‌ی اصلی یافت نشد");

        var reply = new Letter
        {
            LetterType = "Outgoing",
            LetterNumber = letterNumber,
            LetterDate = DateTime.Now,
            Receiver = receiver ?? parent.Sender,
            Subject = subject,
            Body = _sanitizer.Sanitize(body),
            Priority = parent.Priority,
            ConfidentialityLevel = parent.ConfidentialityLevel,
            ProjectId = parent.ProjectId,
            DisciplineId = parent.DisciplineId,
            ParentLetterId = parent.LetterId,
            RegisteredById = registeredById,
            Status = "Draft",
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.Letters.AddAsync(reply);
        await _context.SaveChangesAsync();

        if (parent.Status == "Registered")
            parent.Status = "Referred";

        await _context.SaveChangesAsync();

        await IndexLetterForSemanticSearchAsync(reply);

        return reply.LetterId;
    }


    public async Task AddAttachmentAsync(int letterId, string fileName, string fileExtension, string filePath,
        long fileSize, string? mimeType, int? uploadedById)
    {
        await _context.DocumentAttachments.AddAsync(new DocumentAttachment
        {
            LetterId = letterId,
            FileName = fileName,
            FileExtension = fileExtension,
            FilePath = filePath,
            FileSize = fileSize,
            MimeType = mimeType,
            UploadedById = uploadedById,
            UploadDate = DateTime.Now,
            IsActive = true,
            AIStatus = "NotApplicable"
        });

        await _context.SaveChangesAsync();
    }
}
