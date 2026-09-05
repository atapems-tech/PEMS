using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface ILetterReferralService
{
    Task<int> CreateAsync(int letterId, int? referredToEmployeeId, int? referredToDepartmentId,
        string? instructions, DateTime? dueDate, string? referredByName);

    Task<List<LetterReferralDto>> GetForLetterAsync(int letterId);

    Task<List<LetterDto>> GetMyReferralsAsync(int applicationUserId, bool openOnly = true);

    Task MarkReadAsync(int referralId, int applicationUserId);

    Task CompleteAsync(int referralId, string? responseNote);
}
