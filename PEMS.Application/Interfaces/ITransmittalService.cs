using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface ITransmittalService
{
    Task<List<TransmittalDto>> GetAllAsync();
    Task<TransmittalDto?> GetByIdAsync(int id);

    Task<int> CreateAsync(string transmittalNo, int projectId, string from, string to,
        string purpose, string? description, List<int> documentIds,
        bool responseRequired, List<TransmittalRecipientInput> recipients);

    /// <summary>
    /// طبق بخش ۶ (Due Date) و ۸ (Immutability): لحظه‌ی واقعی صدور رسمی - بعد از این، رکورد
    /// دیگر Draft نیست و قابل ویرایش بی‌صدا نیست. اگر ResponseRequired=true باشد، ResponseDueDate
    /// از روی WorkingDayCalculator موجود محاسبه و اجباراً ثبت می‌شود.
    /// </summary>
    Task MarkSentAsync(int id, int responseDueWorkingDays = 10);

    /// <summary>طبق بخش ۸: مسیر اصلاحیِ قابل‌ردیابی - ویرایش بی‌صدای رکورد صادرشده مجاز نیست.</summary>
    Task CancelAsync(int id, string reason, int? actorEmployeeId);

    /// <summary>یک ترانسمیتال Cancel‌شده را به‌عنوان پیش‌نویس جدید (زنجیره‌ی Supersedes) بازسازی می‌کند.</summary>
    Task<int> ReissueAsync(int id, int? actorEmployeeId);

    Task RecordResponseAsync(int id, string? responseNote);
}
