using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

// داده‌های مرجع «درخواست کاتالوگ قطعه» (Part Catalogue Request) - زیر ماژول انبار (Warehouse).
// منبع: فایل‌های Excel سامانه IFS (Technical Class / Commodity Group / Product Family). همه با کلید «کد»
// Upsert می‌شوند (Idempotent) و از صفحه‌ی مدیریت نیز قابل Import مجدد هستند.

/// <summary>Technical Class (مثلاً IR26641 - MOISTENER). ~۴۱۶۱ ردیف؛ جستجو فقط سمت سرور.</summary>
public class PartCatalogueTechnicalClass : BaseEntity
{
    public int TechnicalClassId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? LongDescription { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Commodity Group (برای Group1 و Group2 یک جدول مشترک؛ ۸۵ ردیف). کد در IFS عددی است ولی به‌صورت رشته ذخیره
/// می‌شود (یکنواخت با Product Family که کدهای متنی مثل ASPC دارد).
/// </summary>
public class PartCatalogueCommodityGroup : BaseEntity
{
    public int CommodityGroupId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>فقط در تصاویر دستورالعمل دیده شده و در فایل Excel نیست (مثلاً Firefighting-10870) - نیازمند تأیید.</summary>
    public bool NeedsConfirmation { get; set; }
}

/// <summary>Product Family (۹۳ ردیف؛ کدهای متنی مثل '*', 'ASPC', 'IPCC', 'SPME' هم دارد → رشته).</summary>
public class PartCatalogueProductFamily : BaseEntity
{
    public int ProductFamilyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>Section (واحد درخواست‌کننده، ۲۳ مقدار ثابت دستورالعمل).</summary>
public class PartCatalogueSection : BaseEntity
{
    public int SectionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? NameFa { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// شاخه‌ی «Section» فلوچارت: بخش‌های Service/Security/... مستقیم به تصمیم Manufacturer جدید می‌روند؛ سایر بخش‌ها
    /// ابتدا «بررسی کارشناس قطعات» دارند. فلوچارت فهرست بخش‌های اول را کامل نمی‌دهد ("Service, Security ...") →
    /// قابل‌ویرایش توسط مدیر و پیش‌فرض‌ها «نیازمند تأیید» هستند.
    /// </summary>
    public bool RequiresSparePartsReview { get; set; } = true;

    public bool RoutingNeedsConfirmation { get; set; }
}

public class PartCatalogueManufacturer : BaseEntity
{
    public int ManufacturerId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>نام نرمال‌شده (حروف بزرگ، بدون فاصله/علامت) برای تشخیص تکراری/مشابه.</summary>
    public string NormalizedName { get; set; } = string.Empty;

    /// <summary>سازنده‌ی داخلی/نامشخص (نام LOCAL) → شماره‌ی قطعه سازنده NO_PART_NR می‌شود.</summary>
    public bool IsLocal { get; set; }

    /// <summary>نام‌هایی که در انتهایشان «(wrong)» دارد؛ ذخیره می‌شوند ولی در لیست انتخاب نمی‌آیند.</summary>
    public bool IsWrong { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PartCatalogueSupplier : BaseEntity
{
    public int SupplierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class PartCatalogueCurrency : BaseEntity
{
    public int CurrencyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameFa { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Stock Grade ۱..۳ (۱=مصرفی، ۲=نیاز به تعویض/احتمال خرابی، ۳=سرمایه‌ای/بیمه‌ای).</summary>
public class PartCatalogueStockGrade : BaseEntity
{
    public int StockGradeId { get; set; }
    public int Grade { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? DescriptionFa { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Planning Method (A,B,C,D,E,F,G,K,M,N,P,T). A ⇒ حداقل موجودی و نرخ مصرف صفر؛ B ⇒ مقدار کاربر.</summary>
public class PartCataloguePlanningMethod : BaseEntity
{
    public int PlanningMethodId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Type Designation (Capital Part، Critical Part، CAP-CR، CR-RE-LOCAL ...). Is Critical / Is Capital از همین ردیف
/// مشتق می‌شود و در فرم فقط‌خواندنی است.
/// </summary>
public class PartCatalogueTypeDesignation : BaseEntity
{
    public int TypeDesignationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
    public bool IsCapital { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Section → Commodity Group1 مجاز (قابل‌ویرایش؛ بدون ردیف برای یک Section یعنی همه‌ی گروه‌ها).</summary>
public class PartCatalogueSectionCommodityGroup : BaseEntity
{
    public int SectionCommodityGroupId { get; set; }
    public int SectionId { get; set; }
    public PartCatalogueSection? Section { get; set; }
    public int CommodityGroupId { get; set; }
    public PartCatalogueCommodityGroup? CommodityGroup { get; set; }
    public bool NeedsConfirmation { get; set; }
}

/// <summary>Commodity Group1 → Group2 مجاز (بدون ردیف یعنی همه‌ی گروه‌ها).</summary>
public class PartCatalogueCommodityGroupChild : BaseEntity
{
    public int CommodityGroupChildId { get; set; }
    public int ParentGroupId { get; set; }
    public PartCatalogueCommodityGroup? ParentGroup { get; set; }
    public int ChildGroupId { get; set; }
    public PartCatalogueCommodityGroup? ChildGroup { get; set; }
    public bool NeedsConfirmation { get; set; }
}
