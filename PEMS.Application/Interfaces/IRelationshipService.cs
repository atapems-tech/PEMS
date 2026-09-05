using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// AI Relationship Engine طبق سند AI Engineering Intelligence Redesign (بخش ۹):
/// زنجیره‌ی کامل روابط مهندسی را - در حد امکان از روی FK های موجود (قطعی و بدون خطا) - می‌سازد،
/// به‌علاوه امکان ثبت روابطی که AI کشف کرده ولی FK صریحی برایشان نیست.
/// </summary>
public interface IRelationshipService
{
    Task<RelationshipExplorationDto?> ExploreFromEquipmentAsync(int equipmentId);

    Task<RelationshipExplorationDto?> ExploreFromDocumentAsync(int documentId);

    Task<RelationshipExplorationDto?> ExploreFromVendorAsync(int vendorId);

    // جستجوی یک تگ تجهیز/شماره مدرک/نام Vendor آزاد در متن سوال کاربر - برای Chat Assistant
    Task<RelationshipExplorationDto?> ExploreByFreeTextAsync(string text);

    Task AddAIProposedRelationshipAsync(string sourceType, int sourceId, string targetType, int targetId,
        string relationshipType, double confidence);

    // ==============================================================================
    // تایید یک پیشنهاد اتصال (SuggestedLinks) - فقط این‌جا FK واقعی برقرار می‌شود؛ خودِ
    // Explore هرگز داده‌ی جدید نمی‌سازد یا FK را خودکار برقرار نمی‌کند.
    // targetType: "MR" یا "VDR". طبق ممیزی بند ۱.۴: اگر هدف قبلاً به رکورد دیگری وصل بوده،
    // بدون force=true رد می‌شود (خطای صریح) - نه بازنویسی بی‌صدا.
    // ==============================================================================
    Task ConfirmSuggestedLinkAsync(int equipmentId, string targetType, int targetId, bool force = false);

    // طبق ممیزی بند ۲.۱: دید تحلیل شکاف برای برنامه‌ریزی - کدام تجهیزات هنوز وصل نشده‌اند
    Task<RelationshipGapReportDto> GetGapReportAsync(int? projectId = null);

    // ==============================================================================
    // طبق درخواست: تعداد تجهیزات زیاد است - تایید یکی‌یکی عملی نیست. این‌جا همه‌ی پیشنهادهای
    // کل سیستم (یا یک پروژه) یک‌جا اسکن و برگردانده می‌شوند تا کاربر دسته‌جمعی تایید کند.
    // ==============================================================================
    Task<List<BulkSuggestedLinkItemDto>> GetAllSuggestedLinksAsync(int? projectId = null);

    // تایید دسته‌جمعی - هرکدام مستقل پردازش می‌شود؛ اگر یکی خطا بدهد بقیه متوقف نمی‌شوند
    Task<(int Confirmed, List<string> Errors)> BulkConfirmAsync(
        IReadOnlyList<(int EquipmentId, string TargetType, int TargetId)> items);
}
