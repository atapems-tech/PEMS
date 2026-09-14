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

    // طبق «PEMS Phase 2A — Global Search Security»: Providerهایی که باید همون مجوز/محدودیت
    // دیسیپلینی صفحه‌ی اصلی موجودیت را تکرار کنند (مثلاً LetterSearchProvider باید همون
    // قانون محرمانگی LetterService.SearchAsync را رعایت کند) به نقش‌های کاربر جاری نیاز
    // دارند - IPermissionService.HasPermissionAsync/IAccessControlService.AuthorizeForRolesAsync
    // هر دو IEnumerable<string> roles می‌گیرند، نه UserId.
    IReadOnlyList<string> Roles { get; }
}
