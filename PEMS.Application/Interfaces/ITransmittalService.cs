using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface ITransmittalService
{
    Task<List<TransmittalDto>> GetAllAsync();

    // طبق «PEMS — Global Grid Hardening» P1: قبلاً GetAllAsync کل جدول Transmittals را
    // بارگذاری می‌کرد و برای محاسبه‌ی SupersededBy یک بار دیگر کل جدول را می‌خواند (دو بار
    // Full-Table Scan روی هر درخواست Index). این متد صفحه‌بندی واقعی سمت SQL دارد.
    Task<PEMS.Application.DTOs.PagedResult<TransmittalDto>> GetPagedAsync(int page, int pageSize);

    Task<TransmittalDto?> GetByIdAsync(int id);

    /// <summary>
    /// طبق «PEMS Phase 2A — EDMS Foundation»: برای تب/بخش «Related Transmittals» روی صفحه‌ی
    /// Engineering Document Details - یک Query هدفمند (نه GetAllAsync + فیلتر در حافظه).
    /// </summary>
    Task<List<RelatedTransmittalDto>> GetForDocumentAsync(int documentId);

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

    /// <summary>
    /// طبق «PEMS EDMS Phase 3 Transmittal Audit» یافته G-22: تایید دریافت مخصوص یک گیرنده -
    /// جدا از RecordResponseAsync (پاسخ رسمی کل Transmittal). فقط برای Transmittal صادرشده
    /// (Sent/Acknowledged) مجاز است؛ تایید تکراری همان گیرنده رد می‌شود. اگر با این تایید همه‌ی
    /// گیرندگان فعال تایید کرده باشند، خودِ Transmittal.Status به "Acknowledged" تغییر می‌کند -
    /// هرگز به‌خاطر یک گیرنده.
    /// </summary>
    Task AcknowledgeAsync(int transmittalRecipientId, string? note, int? actorEmployeeId);
}
