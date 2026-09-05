using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// یک چرخه‌ی ارسال/بازبینی برای یک VDRItem (معادل هر بلوک REV در فایل اکسل قدیمی،
/// اما این‌جا به‌صورت یک ردیف مجزا و بدون محدودیت تعداد رویژن ذخیره می‌شود).
/// </summary>
public class VDRSubmission
{
    [Key]
    public int VDRSubmissionId { get; set; }

    public int VDRItemId { get; set; }
    public VDRItem? VDRItem { get; set; }

    public int RevisionNo { get; set; }

    public string? VendorTransmittalNo { get; set; }

    // طبق درخواست: شماره‌ی Transmittal که ما (پیمانکار EPC) برای ارسال این مدرک به کارفرما/
    // پروژه استفاده کرده‌ایم - جدا از VendorTransmittalNo که مرجع خودِ وندور است
    public string? ClientTransmittalNo { get; set; }

    public int? ClientTransmittalId { get; set; }
    public Transmittal? ClientTransmittal { get; set; }

    public DateTime? VendorActualIssuedDate { get; set; }

    // IFA, IFR, IFI, FI طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» بخش ۴.۶.۱ (همان مفهوم
    // EngineeringDocument.IssuePurpose برای مدارک فروشنده)؛ AB, AFD مقادیر قدیمی این فیلد.
    public string? PurposeOfIssueCode { get; set; }

    public string? ClientCommentSheetNo { get; set; }

    public DateTime? ClientCommentDate { get; set; }

    // AP, NC, AC, CO, RE
    public string? ReviewCategory { get; set; }

    public string? ReplySheetNo { get; set; }

    public DateTime? ReplySheetDate { get; set; }

    // در صورت آپلود فایل واقعی مدرک برای این چرخه (برای تحلیل AI و مطابقت خودکار)
    public int? AttachmentId { get; set; }
    public DocumentAttachment? Attachment { get; set; }

    // طبق درخواست: کاربر باید بتواند خودِ فایل Comment Sheet و Reply Sheet را پیوست کند -
    // قبلاً فقط شماره‌ی این‌ها به‌صورت متن ذخیره می‌شد (ClientCommentSheetNo/ReplySheetNo)،
    // بدون امکان پیوست فایل واقعی
    public int? CommentSheetAttachmentId { get; set; }
    public DocumentAttachment? CommentSheetAttachment { get; set; }

    public int? ReplySheetAttachmentId { get; set; }
    public DocumentAttachment? ReplySheetAttachment { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // طبق بخش ۵.۲ سند: تأییدیهٔ آپلود واقعی روی FTP Server. طبق تصمیم کاربر (اپلیکیشن آفلاین
    // است، نیازی به ایمیل نیست)، این‌جا جایگزین «ایمیل اطلاع‌رسانی» سند به‌عنوان تأییدیهٔ داخلی
    // عمل می‌کند - آپلود بدون این تاریخ، طبق فلسفهٔ سند، هنوز کامل محسوب نمی‌شود.
    public DateTime? FtpUploadedDate { get; set; }
    public string? FtpRemotePath { get; set; }


    // آیا این چرخه نیاز به ارسال مجدد دارد. طبق رفع باگ ممیزی VDR: فقط CO (Comment/Revise &
    // Resubmit) و RE (Rejected) واقعاً نیاز به ارسال مجدد دارند - AC (Approved with Comments)
    // یعنی می‌تواند ادامه دهد و نباید همراه CO/RE حساب شود (قبلاً این باگ مستقیم روی محاسبه‌ی
    // امتیاز ریسک وندور در VendorRiskService هم اثر می‌گذاشت و بی‌جهت ریسک وندور را بالا می‌برد)
    public bool RequiresResubmission =>
        ReviewCategory is "CO" or "RE";

    // طبق بخش ۵.۶ سند: «CONTRACTOR shall provide comments... within 10 working days». مبنا
    // تاریخ صدور واقعی وندور است؛ تا وقتی کامنت واقعاً ثبت نشده (ClientCommentDate خالی)، اگر
    // امروز از این مهلت گذشته باشد یعنی این چرخه از نظر SLA تأخیر دارد.
    [NotMapped]
    public DateTime? ContractorCommentDueDate =>
        VendorActualIssuedDate.HasValue ? WorkingDayCalculator.AddWorkingDays(VendorActualIssuedDate.Value, 10) : null;

    [NotMapped]
    public bool IsContractorCommentOverdue =>
        ContractorCommentDueDate.HasValue && ClientCommentDate == null && DateTime.Now > ContractorCommentDueDate.Value;

    // طبق بخش ۵.۶: «SUB-CONTRACTOR shall return documents sent as IFA/IFR, within 15 working
    // days from the date of receiving soft copies» - مبنا تاریخ دریافت کامنت‌های CONTRACTOR
    // است (نزدیک‌ترین فیلد موجود به «دریافت نسخه‌ی نرم برای بازگرداندن»).
    [NotMapped]
    public DateTime? VendorReturnDueDate =>
        ClientCommentDate.HasValue ? WorkingDayCalculator.AddWorkingDays(ClientCommentDate.Value, 15) : null;

    [NotMapped]
    public bool IsVendorReturnOverdue =>
        VendorReturnDueDate.HasValue && ReplySheetDate == null && DateTime.Now > VendorReturnDueDate.Value;
}
