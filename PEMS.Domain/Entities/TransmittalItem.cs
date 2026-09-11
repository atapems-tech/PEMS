using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// یک ردیف مدرک داخل یک Transmittal (شماره رویژن مدرک در لحظه ارسال ثبت می‌شود).
/// </summary>
public class TransmittalItem
{
    [Key]
    public int TransmittalItemId { get; set; }

    public int TransmittalId { get; set; }
    public Transmittal? Transmittal { get; set; }

    public int DocumentId { get; set; }

    // طبق درخواست: Transmittal باید بتواند هم مدارک مهندسی، هم اقلام VDR را در بر بگیرد -
    // قبلاً فقط به EngineeringDocument محدود بود (سازگار با هسته‌ی مشترک Document Core)
    public Document? Document { get; set; }

    public string RevisionNo { get; set; } = "00";

    public string? Remarks { get; set; }

    // ==============================================================================
    // طبق «PEMS EDMS Phase 3 Transmittal Audit» یافته G-21: پیش از این فقط RevisionNo (رشته)
    // در لحظه‌ی ساخت ذخیره می‌شد - که خودش درست و تغییرناپذیر است (تست موجود
    // CreateAsync_SnapshotsRevisionNoAtTimeOfCreation این را اثبات می‌کند) اما هیچ ارجاعی به
    // خودِ DocumentRevision یا فایل رسمی‌اش نداشت. این فیلدها هم‌زمان با RevisionNo (در همان
    // لحظه‌ی ساخت آیتم، نه دیرتر) پر می‌شوند و دیگر هرگز تغییر نمی‌کنند - even ReissueAsync آن‌ها
    // را عیناً کپی می‌کند، نه این‌که دوباره Resolve کند.
    // ==============================================================================

    public int? DocumentRevisionId { get; set; }
    public DocumentRevision? DocumentRevision { get; set; }

    public int? DocumentAttachmentId { get; set; }
    public DocumentAttachment? DocumentAttachment { get; set; }

    // کپی غیرقابل‌تغییر از هویت فایل رسمی در لحظه‌ی ارسال - عمداً از DocumentAttachment جدا
    // نگه داشته شده تا حتی اگر (نظری) DocumentAttachment بعداً حذف/جایگزین شود، خودِ رکورد
    // تاریخی Transmittal دست‌نخورده بماند
    public string? SnapshotFileName { get; set; }
    public string? SnapshotFileHash { get; set; }
    public long? SnapshotFileSize { get; set; }
}
