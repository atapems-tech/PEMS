using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class HelpCategoryConfiguration : IEntityTypeConfiguration<HelpCategory>
{
    public void Configure(EntityTypeBuilder<HelpCategory> builder)
    {
        builder.ToTable("HelpCategories");
        builder.HasKey(x => x.HelpCategoryId);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NameFa).HasMaxLength(150).IsRequired();
        builder.Property(x => x.IconCssClass).HasMaxLength(50);

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class HelpArticleConfiguration : IEntityTypeConfiguration<HelpArticle>
{
    public void Configure(EntityTypeBuilder<HelpArticle> builder)
    {
        builder.ToTable("HelpArticles");
        builder.HasKey(x => x.HelpArticleId);

        builder.Property(x => x.Code).HasMaxLength(150).IsRequired();
        builder.Property(x => x.ArticleType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.HelpKey).HasMaxLength(200);
        builder.Property(x => x.RelevantRoles).HasMaxLength(500);

        // کد مقاله باید یکتا باشد - هم برای Seed ایدمپوتنت (طبق بخش ۱۵ سند)، هم برای جلوگیری
        // از تداخل در HelpKey های صریح
        builder.HasIndex(x => x.Code).IsUnique();

        // یک HelpKey می‌تواند روی چند مقاله (مثلاً یک Page + چند Field همون صفحه) باشد - پس
        // Unique نیست، فقط Index برای جستجوی سریع در ResolveByContext
        builder.HasIndex(x => x.HelpKey);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.HelpCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class HelpArticleTranslationConfiguration : IEntityTypeConfiguration<HelpArticleTranslation>
{
    public void Configure(EntityTypeBuilder<HelpArticleTranslation> builder)
    {
        builder.ToTable("HelpArticleTranslations");
        builder.HasKey(x => x.HelpArticleTranslationId);

        builder.Property(x => x.LanguageCode).HasMaxLength(5).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.SearchKeywords).HasMaxLength(1000);

        // هر مقاله دقیقاً یک ترجمه به‌ازای هر زبان دارد - نه بیشتر
        builder.HasIndex(x => new { x.HelpArticleId, x.LanguageCode }).IsUnique();

        builder.HasOne(x => x.Article)
            .WithMany(x => x.Translations)
            .HasForeignKey(x => x.HelpArticleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class HelpVisualConfiguration : IEntityTypeConfiguration<HelpVisual>
{
    public void Configure(EntityTypeBuilder<HelpVisual> builder)
    {
        builder.ToTable("HelpVisuals");
        builder.HasKey(x => x.HelpVisualId);

        builder.Property(x => x.VisualType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.SourceWorkflowModule).HasMaxLength(100);
        builder.Property(x => x.SourceWorkflowCategory).HasMaxLength(100);
        builder.Property(x => x.DefinitionJson);
        builder.Property(x => x.CaptionEn).HasMaxLength(500).IsRequired();
        builder.Property(x => x.CaptionFa).HasMaxLength(500).IsRequired();
        builder.Property(x => x.LegendEn).HasMaxLength(1000);
        builder.Property(x => x.LegendFa).HasMaxLength(1000);
        builder.Property(x => x.AccessibilityDescriptionEn).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.AccessibilityDescriptionFa).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.RelatedHelpKey).HasMaxLength(200);

        builder.HasIndex(x => x.HelpArticleId);

        builder.HasOne(x => x.Article)
            .WithMany(a => a.Visuals)
            .HasForeignKey(x => x.HelpArticleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class HelpStandardReferenceConfiguration : IEntityTypeConfiguration<HelpStandardReference>
{
    public void Configure(EntityTypeBuilder<HelpStandardReference> builder)
    {
        builder.ToTable("HelpStandardReferences");
        builder.HasKey(x => x.HelpStandardReferenceId);

        builder.Property(x => x.Identifier).HasMaxLength(50).IsRequired();
        builder.Property(x => x.IssuingOrganization).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EditionOrYear).HasMaxLength(200);
        builder.Property(x => x.OfficialReferenceUrl).HasMaxLength(500);
        builder.Property(x => x.RelatedModule).HasMaxLength(200);
        builder.Property(x => x.ApplicabilityStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.VerificationStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.PEMSImplementationStatus).HasMaxLength(40).IsRequired();
        builder.Property(x => x.EvidencePath).HasMaxLength(300);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        // طبق بخش ۶ سند («Standard uniqueness»): دو رکورد نمی‌توانند یک شناسه‌ی رسمی یکسان
        // (مثلاً "ISO 14224") داشته باشند
        builder.HasIndex(x => x.Identifier).IsUnique();

        // یک‌به‌یک واقعی با HelpArticle - هر مقاله حداکثر یک رکورد Standard Reference دارد
        builder.HasIndex(x => x.HelpArticleId).IsUnique();

        builder.HasOne(x => x.Article)
            .WithOne(a => a.StandardReference)
            .HasForeignKey<HelpStandardReference>(x => x.HelpArticleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class HelpRelatedArticleConfiguration : IEntityTypeConfiguration<HelpRelatedArticle>
{
    public void Configure(EntityTypeBuilder<HelpRelatedArticle> builder)
    {
        builder.ToTable("HelpRelatedArticles");
        builder.HasKey(x => x.HelpRelatedArticleId);

        builder.HasIndex(x => new { x.HelpArticleId, x.RelatedHelpArticleId }).IsUnique();

        builder.HasOne(x => x.Article)
            .WithMany(x => x.RelatedArticles)
            .HasForeignKey(x => x.HelpArticleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RelatedArticle)
            .WithMany()
            .HasForeignKey(x => x.RelatedHelpArticleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
