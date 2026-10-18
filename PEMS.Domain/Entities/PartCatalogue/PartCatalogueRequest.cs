using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

public static class PartCatalogueRequestTypes
{
    public const string Add = "Add";
    public const string Change = "Change";
}

public static class PartCatalogueStatuses
{
    /// <summary>ثبت‌شده ولی مدیر مستقیم از ساختار سازمانی قابل‌تعیین نیست («Organization Resolution Required»)؛ پس از اصلاح ساختار با «تلاش مجدد مسیریابی» ادامه می‌یابد.</summary>
    public const string OrganizationResolutionRequired = "OrgResolution";
    public const string Draft = "Draft";

    /// <summary>در جریان تأیید (گام جاری در CurrentStepKey).</summary>
    public const string InApproval = "InApproval";

    /// <summary>برگشت برای اصلاح - فقط درخواست‌دهنده ویرایش و دوباره ثبت می‌کند.</summary>
    public const string Returned = "Returned";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Draft, InApproval, Returned, Completed, Cancelled };
}

public static class PartCatalogueAssetTypes
{
    public const string None = "None";
    public const string FixedAsset = "FixedAsset";
    public const string LowValueAsset = "LowValueAsset";
}

/// <summary>
/// درخواست ایجاد (Add) / ویرایش (Change) کالا در کاتالوگ - Add = ۳۴ فیلد، Change = ۴ فیلد اضافه
/// (دستورالعمل). متعلق به ماژول «انبار». شماره‌ی درخواست با کلیک روی دکمه «شماره‌دهی» تولید می‌شود (نه خودکار هنگام ایجاد).
/// </summary>
public class PartCatalogueRequest : BaseEntity
{
    public int PartCatalogueRequestId { get; set; }

    // ---- Part Info

    /// <summary>۱ - Request Number (تولید سیستمی با دکمه؛ یکتا؛ تا قبل از شماره‌دهی null).</summary>
    public string? RequestNumber { get; set; }
    public string RequestType { get; set; } = PartCatalogueRequestTypes.Add;
    public string Status { get; set; } = PartCatalogueStatuses.Draft;
    public string? CurrentStepKey { get; set; }

    /// <summary>
    /// WorkflowInstance جاری/آخر در PEMS.WorkflowPlatform (مرجع نهایی مسیر تأیید). null = درخواست Legacy که با مسیریاب قدیمی
    /// ماژول (PartCatalogueRoutingPlanner) ثبت شده؛ Steps/Approvals فقط Projection خواندنی برای UI/چاپ هستند.
    /// </summary>
    public int? WorkflowInstanceId { get; set; }

    /// <summary>دلیل توقف مسیریابی (کد تشخیصی سازمانی) وقتی Status = OrgResolution؛ در غیر این‌صورت null.</summary>
    public string? RoutingBlockReason { get; set; }

    /// <summary>۲ - Urgent (فوری ⇒ برجسته و مرتب‌شده در ابتدای صف‌ها).</summary>
    public bool Urgent { get; set; }

    /// <summary>۳ - Section.</summary>
    public int? SectionId { get; set; }
    public PartCatalogueSection? Section { get; set; }

    /// <summary>درخواست‌دهنده - EmployeeId (نه UserId؛ الگوی GetCurrentEmployeeIdAsync).</summary>
    public int RequesterEmployeeId { get; set; }
    public Employee? Requester { get; set; }

    // ---- Equipment/Object ID

    /// <summary>۵ - Is New Equipment (تجهیز جدید ⇒ متن آزاد به‌جای انتخاب از لیست).</summary>
    public bool IsNewEquipment { get; set; }

    // ---- Class Info

    /// <summary>۶ - Technical Class.</summary>
    public int? TechnicalClassId { get; set; }
    public PartCatalogueTechnicalClass? TechnicalClass { get; set; }

    // ---- Part Classification

    /// <summary>۸ - None | FixedAsset | LowValueAsset (Low Value ⇒ اقلام مصرفیِ کم‌ارزش تا آستانه‌ی قابل‌تنظیم).</summary>
    public string AssetType { get; set; } = PartCatalogueAssetTypes.None;
    public int? CommodityGroup1Id { get; set; }
    public PartCatalogueCommodityGroup? CommodityGroup1 { get; set; }
    public int? CommodityGroup2Id { get; set; }
    public PartCatalogueCommodityGroup? CommodityGroup2 { get; set; }
    public int? ProductFamilyId { get; set; }
    public PartCatalogueProductFamily? ProductFamily { get; set; }

    /// <summary>۱۲ - Unit Of Measure (از MaterialUOM موجود استفاده می‌شود - نه جدول تکراری).</summary>
    public int? UnitOfMeasureId { get; set; }
    public MaterialUOM? UnitOfMeasure { get; set; }
    public int? ManufacturerId { get; set; }
    public PartCatalogueManufacturer? Manufacturer { get; set; }

    /// <summary>۱۴ - Is New Manufacturer (⇒ NewManufacturerName الزامی، Latin، حروف بزرگ).</summary>
    public bool IsNewManufacturer { get; set; }
    public string? NewManufacturerName { get; set; }
    public string? ManufacturerPartNumber { get; set; }
    public int? SupplierId { get; set; }
    public PartCatalogueSupplier? Supplier { get; set; }
    public string? SupplierPartNumber { get; set; }

    /// <summary>۱۸ - قیمت تقریبی توسط درخواست‌دهنده + ارز.</summary>
    public decimal? PartPrice { get; set; }
    public int? CurrencyId { get; set; }
    public PartCatalogueCurrency? Currency { get; set; }
    public int? TypeDesignationId { get; set; }
    public PartCatalogueTypeDesignation? TypeDesignation { get; set; }

    /// <summary>۲۰/۲۱ - مشتق از Type Designation؛ فقط‌خواندنی (در سرویس محاسبه و ذخیره می‌شود).</summary>
    public bool IsCritical { get; set; }
    public bool IsCapital { get; set; }

    // ---- Other Info
    public string? SparePartSerialNo { get; set; }
    public string? EquipmentSerialNo { get; set; }
    public string? ManufacturerDrawingNo { get; set; }
    public string? DocumentNo { get; set; }

    // ---- Inventory Planning
    public decimal? MinimumStock { get; set; }
    public decimal? MaxConsumptionRate { get; set; }
    public decimal? FirstPurchaseQuantity { get; set; }
    public int? PlanningMethodId { get; set; }
    public PartCataloguePlanningMethod? PlanningMethod { get; set; }
    public bool PrimaryIssuedRequired { get; set; }
    public decimal? IssuedQty { get; set; }
    public int? StockGradeId { get; set; }
    public PartCatalogueStockGrade? StockGrade { get; set; }

    // ---- فقط برای چاپ فرم INWH-PLA-FO-001 (در ۳۴ فیلد دستورالعمل نیست؛ اختیاری)
    public DateTime? WantedReceiptDate { get; set; }
    public string? StorageVolume { get; set; }

    // ---- Change

    /// <summary>Change-۱ IS RD (قطعه در R&amp;D موجود است).</summary>
    public bool IsRd { get; set; }

    /// <summary>Change-۲ Part Number (شماره‌ی قطعه‌ی موجود؛ با Load فیلدها پر می‌شود).</summary>
    public string? ExistingPartNumber { get; set; }

    /// <summary>Change-۳ Changes match the inventory of goods (No ⇒ مستندات پیوست الزامی).</summary>
    public bool? ChangesMatchInventory { get; set; }

    /// <summary>Change-۴ سازنده/شماره‌ی قطعه‌ی قبلی (برای افزودن خودکار به FREE FORMAT TEXT).</summary>
    public string? OldManufacturerName { get; set; }
    public string? OldManufacturerPartNumber { get; set; }

    // ---- خروجی

    /// <summary>شماره‌ی قطعه‌ی ثبت‌شده در IFS (توسط Part Cataloguer پس از ثبت در IFS وارد می‌شود).</summary>
    public string? AssignedPartNumber { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public bool IsActive { get; set; } = true;

    public ICollection<PartCatalogueRequestEquipment> Equipment { get; set; } = new List<PartCatalogueRequestEquipment>();
    public ICollection<PartCatalogueRequestAttribute> Attributes { get; set; } = new List<PartCatalogueRequestAttribute>();
    public ICollection<PartCatalogueRequestNote> Notes { get; set; } = new List<PartCatalogueRequestNote>();
    public ICollection<PartCatalogueRequestAttachment> Attachments { get; set; } = new List<PartCatalogueRequestAttachment>();
    public ICollection<PartCatalogueRequestStep> Steps { get; set; } = new List<PartCatalogueRequestStep>();
    public ICollection<PartCatalogueRequestApproval> Approvals { get; set; } = new List<PartCatalogueRequestApproval>();
}

/// <summary>۴ - Equipment of parts: تجهیز + تعداد نصب‌شده (یا متن آزاد وقتی Is New Equipment).</summary>
public class PartCatalogueRequestEquipment : BaseEntity
{
    public int RequestEquipmentId { get; set; }
    public int PartCatalogueRequestId { get; set; }
    public PartCatalogueRequest? Request { get; set; }
    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }
    public string? EquipmentText { get; set; }
    public decimal InstalledQuantity { get; set; }
}

/// <summary>۷ - Attribute Values. ردیف «FREE FORMAT TEXT» اجباری است (تک‌خطی، با فاصله‌ی ابتدایی).</summary>
public class PartCatalogueRequestAttribute : BaseEntity
{
    public const string FreeFormatText = "FREE FORMAT TEXT";

    public int RequestAttributeId { get; set; }
    public int PartCatalogueRequestId { get; set; }
    public PartCatalogueRequest? Request { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>۳۴ - توضیحات: پاسخ درخواست‌دهنده به برگشت‌ها + یادداشت بازبین‌ها (نمایش گام و نویسنده).</summary>
public class PartCatalogueRequestNote : BaseEntity
{
    public int RequestNoteId { get; set; }
    public int PartCatalogueRequestId { get; set; }
    public PartCatalogueRequest? Request { get; set; }
    public string Text { get; set; } = string.Empty;
    public int AuthorEmployeeId { get; set; }
    public Employee? Author { get; set; }
    public string? StepKey { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}

/// <summary>۲۶ - Document Attachment (چندفایل). Kind: Document | Drawing | Support (مستندات تأیید Change).</summary>
public class PartCatalogueRequestAttachment : BaseEntity
{
    public int RequestAttachmentId { get; set; }
    public int PartCatalogueRequestId { get; set; }
    public PartCatalogueRequest? Request { get; set; }
    public string FileName { get; set; } = string.Empty;

    /// <summary>کلید ذخیره‌سازی از IFileStorageService (نام فیزیکی GUID).</summary>
    public string StorageKey { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public string? FileHash { get; set; }
    public string Kind { get; set; } = "Document";
    public int UploadedByEmployeeId { get; set; }
    public DateTime UploadedDate { get; set; } = DateTime.Now;
    public bool IsActive { get; set; } = true;
}

/// <summary>مسیر برنامه‌ریزی‌شده‌ی همین درخواست (Snapshot هنگام Submit): هر گام فعال یا با دلیل Skip می‌شود.</summary>
public class PartCatalogueRequestStep : BaseEntity
{
    public const string Pending = "Pending";
    public const string Active = "Active";
    public const string Approved = "Approved";
    public const string Skipped = "Skipped";

    public int RequestStepId { get; set; }
    public int PartCatalogueRequestId { get; set; }
    public PartCatalogueRequest? Request { get; set; }
    public string StepKey { get; set; } = string.Empty;
    public int StepOrder { get; set; }
    public string Status { get; set; } = Pending;
    public string? SkipReason { get; set; }

    /// <summary>برای گام‌های مبتنی بر مدیر: شخصِ حل‌شده (مدیر مستقیم/مافوق).</summary>
    public int? AssignedEmployeeId { get; set; }
    public DateTime? ActivatedDate { get; set; }
    public DateTime? DecidedDate { get; set; }
}

/// <summary>تاریخچه‌ی تصمیم‌ها (Append-only): Submit/Approve/Return/Cancel/Resubmit/Skip/RegisterPartNumber/AdminOverride.</summary>
public class PartCatalogueRequestApproval : BaseEntity
{
    public int RequestApprovalId { get; set; }
    public int PartCatalogueRequestId { get; set; }
    public PartCatalogueRequest? Request { get; set; }
    public string? StepKey { get; set; }
    public string Action { get; set; } = string.Empty;
    public int ActorEmployeeId { get; set; }
    public Employee? Actor { get; set; }
    public string? Comment { get; set; }
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public DateTime ActionDate { get; set; } = DateTime.Now;
}

public static class PartCatalogueActions
{
    public const string Submit = "Submit";
    public const string Resubmit = "Resubmit";
    public const string Approve = "Approve";
    public const string Return = "Return";
    public const string Cancel = "Cancel";
    public const string Skip = "Skip";
    public const string RegisterPartNumber = "RegisterPartNumber";
    public const string AdminOverride = "AdminOverride";
    /// <summary>ثبت شد ولی مدیر مستقیم از ساختار سازمانی قابل‌تعیین نیست (Comment = کد مشکل).</summary>
    public const string OrgResolutionRequired = "OrgResolutionRequired";
    /// <summary>تلاش مجدد مسیریابی پس از اصلاح ساختار سازمانی.</summary>
    public const string RetryRouting = "RetryRouting";
}
