namespace PEMS.Application.Interfaces;

/// <summary>
/// ==============================================================================
/// چرا این Abstraction لازم است: Provider های جستجو (مثل NotificationSearchProvider) در
/// PEMS.Persistence هستند و برای فیلتر کردن نتایج به کاربر (مثلاً نوتیفیکیشن‌های خودِ همون
/// کاربر) نیاز به شناسه‌ی کاربر جاری دارند. ICurrentUserService واقعی در PEMS.Web است و
/// PEMS.Persistence نباید به PEMS.Web وابسته شود (خلاف جهت صحیح Clean Architecture). طبق
/// همون الگوی IWorkflowDbContext: این Interface کوچک این‌جا (لایه‌ای که استفاده‌اش می‌کند)
/// تعریف می‌شود و در PEMS.Web (لایه‌ای که واقعاً کاربر لاگین‌شده را می‌شناسد) پیاده‌سازی می‌شود.
/// ==============================================================================
/// </summary>
public interface ICurrentUserContext
{
    int? UserId { get; }
    int? EmployeeId { get; }
}
