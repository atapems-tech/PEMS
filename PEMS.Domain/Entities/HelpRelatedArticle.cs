namespace PEMS.Domain.Entities;

/// <summary>
/// رابطه‌ی «مقاله‌ی مرتبط» بین دو HelpArticle - یک جدول اتصال ساده و خودارجاع، دقیقاً به همون
/// سادگی DocumentIdcReferral (بدون BaseEntity - چون خودش موجودیت کسب‌وکاری مستقلی نیست، فقط
/// یک رابطه‌ی دوطرفه‌ی صریح است).
/// </summary>
public class HelpRelatedArticle
{
    public int HelpRelatedArticleId { get; set; }

    public int HelpArticleId { get; set; }
    public HelpArticle? Article { get; set; }

    public int RelatedHelpArticleId { get; set; }
    public HelpArticle? RelatedArticle { get; set; }
}
