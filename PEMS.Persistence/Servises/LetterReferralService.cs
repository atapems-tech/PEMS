using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// گردش‌کار ارجاع نامه (Referral Workflow): ارجاع به کاربر/دپارتمان، دستورالعمل، مهلت،
/// ردیابی خوانده/نخوانده - با اعلان خودکار به گیرنده از طریق INotificationService موجود.
/// </summary>
public class LetterReferralService : ILetterReferralService
{
    private readonly PEMSDbContext _context;
    private readonly INotificationService _notificationService;

    public LetterReferralService(PEMSDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<int> CreateAsync(int letterId, int? referredToEmployeeId, int? referredToDepartmentId,
        string? instructions, DateTime? dueDate, string? referredByName)
    {
        var letter = await _context.Letters.FirstOrDefaultAsync(x => x.LetterId == letterId);
        if (letter == null) throw new Exception("نامه یافت نشد");

        var referral = new LetterReferral
        {
            LetterId = letterId,
            ReferredToEmployeeId = referredToEmployeeId,
            ReferredToDepartmentId = referredToDepartmentId,
            Instructions = instructions,
            DueDate = dueDate,
            Status = "Pending",
            IsRead = false,
            ReferredByName = referredByName,
            ReferredDate = DateTime.Now
        };

        await _context.LetterReferrals.AddAsync(referral);

        if (letter.LetterType == "Incoming" && letter.Status == "Registered")
            letter.Status = "Referred";

        await _context.SaveChangesAsync();

        // اعلان خودکار به کاربر ارجاع‌شده (اگر به یک کارمند مشخص ارجاع شده، نه فقط دپارتمان)
        if (referredToEmployeeId.HasValue)
        {
            var targetUserId = await _context.Users
                .Where(x => x.EmployeeId == referredToEmployeeId.Value)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();

            if (targetUserId.HasValue)
            {
                await _notificationService.CreateAsync(
                    targetUserId.Value,
                    "نامه‌ای به شما ارجاع شد",
                    $"{letter.LetterNumber} - {letter.Subject}",
                    $"/Letter/Details/{letterId}",
                    "Letter",
                    letterId);
            }
        }

        return referral.LetterReferralId;
    }

    public async Task<List<LetterReferralDto>> GetForLetterAsync(int letterId)
    {
        var referrals = await _context.LetterReferrals
            .Include(x => x.ReferredToEmployee)
            .Include(x => x.ReferredToDepartment)
            .Where(x => x.LetterId == letterId)
            .OrderByDescending(x => x.ReferredDate)
            .ToListAsync();

        return referrals.Select(r => new LetterReferralDto
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
        }).ToList();
    }

    public async Task<List<LetterDto>> GetMyReferralsAsync(int applicationUserId, bool openOnly = true)
    {
        var employeeId = await _context.Users
            .Where(x => x.Id == applicationUserId)
            .Select(x => x.EmployeeId)
            .FirstOrDefaultAsync();

        if (!employeeId.HasValue)
            return new List<LetterDto>();

        var referralQuery = _context.LetterReferrals
            .Where(x => x.ReferredToEmployeeId == employeeId.Value);

        if (openOnly)
            referralQuery = referralQuery.Where(x => x.Status != "Completed");

        var letterIds = await referralQuery.Select(x => x.LetterId).Distinct().ToListAsync();

        var letters = await _context.Letters
            .Include(x => x.Project)
            .Include(x => x.Discipline)
            .Where(x => letterIds.Contains(x.LetterId))
            .OrderByDescending(x => x.LetterDate)
            .ToListAsync();

        return letters.Select(l => new LetterDto
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
            Status = l.Status,
            CreatedDate = l.CreatedDate
        }).ToList();
    }

    public async Task MarkReadAsync(int referralId, int applicationUserId)
    {
        var referral = await _context.LetterReferrals.FirstOrDefaultAsync(x => x.LetterReferralId == referralId);
        if (referral == null) return;

        referral.IsRead = true;
        referral.ReadDate = DateTime.Now;

        if (referral.Status == "Pending")
            referral.Status = "InProgress";

        await _context.SaveChangesAsync();
    }

    public async Task CompleteAsync(int referralId, string? responseNote)
    {
        var referral = await _context.LetterReferrals.FirstOrDefaultAsync(x => x.LetterReferralId == referralId);
        if (referral == null) return;

        referral.Status = "Completed";
        referral.ResponseNote = responseNote;
        referral.CompletedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }
}
