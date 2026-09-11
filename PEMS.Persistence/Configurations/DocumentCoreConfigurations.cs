using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// پیکربندی Enterprise Document Core (Table-Per-Type) و انواع تخصصی آن -
/// طبق استاندارد جدید (Volume V): یک کلاس به‌ازای هر Entity، همه در یک فایل چون به‌شدت
/// به‌هم مرتبط‌اند (خانواده‌ی Document).
/// </summary>
public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");

        // فیلدهای مشترک (Project, Discipline) این‌جا تنظیم می‌شوند و خودکار برای همه‌ی
        // زیرکلاس‌ها (EngineeringDocument, VDRItem, ...) اعمال می‌شوند
        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Discipline)
            .WithMany()
            .HasForeignKey(x => x.DisciplineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FrozenByEmployee)
            .WithMany()
            .HasForeignKey(x => x.FrozenById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UnfrozenByEmployee)
            .WithMany()
            .HasForeignKey(x => x.UnfrozenById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.IsActive, x.DisciplineId, x.CreatedDate });
        builder.HasIndex(x => new { x.IsActive, x.ProjectId, x.CreatedDate });
        builder.HasIndex(x => new { x.IsActive, x.Status });

        // طبق Phase E-01 (رفع P0 ممیزی): جلوگیری از تکرار شماره‌ی مدرک در سطح دیتابیس - چه از
        // مسیر خودکار (NumberingService اتمیک) چه از مسیر شماره‌گذاری دستی (CanManualNumber).
        // فیلتر روی مقادیر غیرخالی چون بعضی رکوردهای قدیمی/تست ممکن است DocumentNo خالی داشته
        // باشند و SQL Server یک ایندکس Unique معمولی بیش از یک NULL/خالی را رد می‌کند.
        builder.HasIndex(x => x.DocumentNo)
            .IsUnique()
            .HasFilter("[DocumentNo] IS NOT NULL AND [DocumentNo] <> ''");
    }
}

/// <summary>
/// طبق بخش G ممیزی (بند ۲۲ - Numbering Regression: «No duplicate formal transmittal numbers»):
/// TransmittalService.CreateAsync قبلاً فقط با یک AnyAsync برنامه‌ای یکتایی را چک می‌کرد که یک
/// پنجره‌ی Race واقعی دارد (دو درخواست هم‌زمان می‌توانند هر دو از این چک با موفقیت رد شوند، قبل
/// از این‌که هیچ‌کدام Commit شود). دقیقاً هم‌الگو با DocumentNo بالا: یکتایی واقعی فقط با ایندکس
/// Unique سطح دیتابیس تضمین می‌شود.
/// </summary>
public class TransmittalConfiguration : IEntityTypeConfiguration<Transmittal>
{
    public void Configure(EntityTypeBuilder<Transmittal> builder)
    {
        builder.HasIndex(x => x.TransmittalNo)
            .IsUnique()
            .HasFilter("[TransmittalNo] IS NOT NULL AND [TransmittalNo] <> ''");
    }
}

public class EngineeringDocumentConfiguration : IEntityTypeConfiguration<EngineeringDocument>
{
    public void Configure(EntityTypeBuilder<EngineeringDocument> builder)
    {
        builder.ToTable("EngineeringDocuments");

        // طبق Phase E-04 (رفع P1 ممیزی Reverse Traceability): چهار رابطه‌ی زیر از قبل روی
        // دیتابیس نگاشت شده بودند (ستون FK از قبل وجود دارد) - فقط نام Navigation معکوس اضافه
        // شده، بدون نیاز به Migration.
        builder.HasOne(x => x.Vendor)
            .WithMany(x => x.EngineeringDocuments)
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RelatedMR)
            .WithMany(x => x.EngineeringDocuments)
            .HasForeignKey(x => x.RelatedMRId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Equipment)
            .WithMany(x => x.EngineeringDocuments)
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Contract)
            .WithMany(x => x.EngineeringDocuments)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PreparedByEmployee)
            .WithMany()
            .HasForeignKey(x => x.PreparedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CheckedByEmployee)
            .WithMany()
            .HasForeignKey(x => x.CheckedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedByEmployee)
            .WithMany()
            .HasForeignKey(x => x.ApprovedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class VDRItemConfiguration : IEntityTypeConfiguration<VDRItem>
{
    public void Configure(EntityTypeBuilder<VDRItem> builder)
    {
        builder.ToTable("VDRItems");

        builder.HasOne(x => x.MR)
            .WithMany()
            .HasForeignKey(x => x.MRId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vendor)
            .WithMany(x => x.VDRItems)
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Po)
            .WithMany(x => x.VDRItems)
            .HasForeignKey(x => x.PoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.DocumentNumber);
    }
}

public class ContractDocumentConfiguration : IEntityTypeConfiguration<ContractDocument>
{
    public void Configure(EntityTypeBuilder<ContractDocument> builder)
    {
        builder.ToTable("ContractDocuments");

        builder.HasOne(x => x.Contract)
            .WithMany()
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaintenanceDocumentConfiguration : IEntityTypeConfiguration<MaintenanceDocument>
{
    public void Configure(EntityTypeBuilder<MaintenanceDocument> builder)
    {
        builder.ToTable("MaintenanceDocuments");

        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.WorkOrder)
            .WithMany()
            .HasForeignKey(x => x.WorkOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// طبق «PEMS EDMS Deep Repository Audit» یافته F-06 (Revision-File Binding): پیوست یک مدرک اختیاراً
/// می‌تواند به یک رویژن مشخص وصل شود. Restrict (نه Cascade) طبق قرارداد این نشست برای جلوگیری از
/// ریسک چرخه‌ی Cascade چندگانه در SQL Server - حذف یک رویژن نباید پیوست‌هایش را خودکار حذف کند.
/// </summary>
public class DocumentAttachmentRevisionConfiguration : IEntityTypeConfiguration<DocumentAttachment>
{
    public void Configure(EntityTypeBuilder<DocumentAttachment> builder)
    {
        builder.HasOne(x => x.DocumentRevision)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.DocumentRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// طبق «PEMS EDMS Phase 2 Lifecycle Design» بند ۳: پاسخ کارفرما مخصوص یک رویژن - Restrict (نه
/// Cascade) طبق قرارداد این نشست، تا حذف یک نامه هرگز تاریخچه‌ی پاسخ کارفرمای یک رویژن را
/// خودکار پاک نکند.
/// </summary>
public class DocumentRevisionClientReviewConfiguration : IEntityTypeConfiguration<DocumentRevision>
{
    public void Configure(EntityTypeBuilder<DocumentRevision> builder)
    {
        builder.HasOne(x => x.ClientReviewLetter)
            .WithMany()
            .HasForeignKey(x => x.ClientReviewLetterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DocumentCommentConfiguration : IEntityTypeConfiguration<DocumentComment>
{
    public void Configure(EntityTypeBuilder<DocumentComment> builder)
    {
        builder.HasOne(x => x.Document)
            .WithMany(x => x.Comments)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.VDRSubmission)
            .WithMany()
            .HasForeignKey(x => x.VDRSubmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Attachment)
            .WithMany()
            .HasForeignKey(x => x.AttachmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReplyAttachment)
            .WithMany()
            .HasForeignKey(x => x.ReplyAttachmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ParentComment)
            .WithMany()
            .HasForeignKey(x => x.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
