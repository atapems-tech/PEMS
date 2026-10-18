namespace PEMS.Application.DTOs;

/// <summary>
/// یک گره در زنجیره‌ی روابط مهندسی (طبق مثال سند: Pump P-101 → Datasheet → Vendor → P&ID → ...).
/// </summary>
public class RelationshipNodeDto
{
    public string NodeType { get; set; } = ""; // Document, Equipment, Vendor, Contract, MR, MRQ, PurchaseOrder, WorkOrder
    public int NodeId { get; set; }
    public string Label { get; set; } = "";
    public string? SubLabel { get; set; }
    public string RelationshipType { get; set; } = "RelatesTo";
    public bool IsAIProposed { get; set; }

    // طبق ممیزی - دید برنامه‌ریزی/تولید: تاریخ برنامه‌ریزی‌شده/سررسید و اولویت، نه فقط Status خام
    public DateTime? PlannedOrDueDate { get; set; }
    public int? DelayDays { get; set; }
    public string? Priority { get; set; }

    // طبق ممیزی بند ۱.۱: تمایز صریح بین رابطه‌ی تاییدشده (FK واقعی) و رابطه‌ی نامزد
    // (تطبیق متنی، هنوز قطعی نیست) - تا کاربر با داده‌ی احتمالی به‌عنوان قطعی اشتباه نگیره
    public bool IsConfirmed { get; set; } = true;
}

/// <summary>
/// نتیجه‌ی کامل کاوش زنجیره‌ی روابط از یک نقطه‌ی شروع (Equipment، Document یا Vendor).
/// </summary>
public class RelationshipExplorationDto
{
    public string RootType { get; set; } = "";
    public int RootId { get; set; }
    public string RootLabel { get; set; } = "";

    // طبق ممیزی بند ۴.۲: مهندس فرآیند با واحد/سیستم فکر می‌کند، نه فقط تک‌تجهیز
    public string? RootArea { get; set; }
    public bool RootIsActive { get; set; } = true;

    public List<RelationshipNodeDto> Documents { get; set; } = new();
    public List<RelationshipNodeDto> VendorDocuments { get; set; } = new();
    public List<RelationshipNodeDto> Equipment { get; set; } = new();
    public List<RelationshipNodeDto> Vendors { get; set; } = new();
    public List<RelationshipNodeDto> Contracts { get; set; } = new();
    public List<RelationshipNodeDto> PurchaseOrders { get; set; } = new();
    public List<RelationshipNodeDto> MaterialRequisitions { get; set; } = new();
    public List<RelationshipNodeDto> Quotations { get; set; } = new();
    public List<RelationshipNodeDto> WorkOrders { get; set; } = new();

    // طبق ممیزی بند ۴.۱: خلأ بزرگ - P&ID که با تطبیق هوشمند (DocumentNumberMatcher) پیدا
    // می‌شود، قبلاً هیچ‌جای این کاوشگر نبود
    public RelationshipNodeDto? PIDDocument { get; set; }

    // طبق فاز ۳ نقشه‌راه معماری: تعمیق محوریت Asset
    public List<RelationshipNodeDto> NonConformanceReports { get; set; } = new();
    public List<RelationshipNodeDto> MOCs { get; set; } = new();
    public List<RelationshipNodeDto> TechnicalQueries { get; set; } = new();

    // ==============================================================================
    // طبق گزارش سخت‌افزاری نهایی Package Procurement (Gap ۴): زنجیره‌ی Tender/Bid/NTP پیش از
    // این جلسه اصلاً وجود نداشت، پس هیچ‌جای این Explorer قابل مشاهده نبود - بسط معماری موجود،
    // نه یک Explorer دوم.
    // ==============================================================================
    public List<RelationshipNodeDto> Tenders { get; set; } = new();
    public List<RelationshipNodeDto> Bids { get; set; } = new();
    public List<RelationshipNodeDto> ContractNTPs { get; set; } = new();
    public List<RelationshipNodeDto> KickoffMeetings { get; set; } = new();

    // طبق ممیزی نهایی Ownership/Traceability (§16): وقتی کاوش از Contract شروع می‌شود، باید
    // Package مبدا هم دیده شود - نه به‌زور داخل لیست MaterialRequisitions که معنایش چیز دیگری‌ست
    public List<RelationshipNodeDto> ProcurementPackages { get; set; } = new();

    // طبق ممیزی نهایی §13: ITP های وصل‌شده به Contract/Package باید با لینک درست (QC/ItpDetails)
    // دیده شوند - نه داخل «نگهداری/دستور کار» که کنترلر متفاوتی دارد
    public List<RelationshipNodeDto> InspectionTestPlans { get; set; } = new();

    // ==============================================================================
    // پیشنهادهای بازبینی‌نشده (IsAIProposed=true) - از روی MRNumberRaw/PONumberRaw تجهیز با
    // MR/VDRItem های موجود (نه ساخته‌شده) منطبق شده‌اند. تا کاربر تایید نکند
    // (ConfirmSuggestedLinkAsync)، FK واقعی برقرار نمی‌شود.
    // ==============================================================================
    public List<RelationshipNodeDto> SuggestedLinks { get; set; } = new();
}

/// <summary>
/// طبق ممیزی بند ۲.۱: دید «تحلیل شکاف» برای برنامه‌ریزی - کدام تجهیزات هنوز به هیچ مدرک/VDR/MR
/// وصل نیستند. Explorer اصلی فقط «کاوش از یک نقطه‌ی مشخص» است؛ این گزارش برعکسش را می‌دهد.
/// </summary>
public class RelationshipGapItemDto
{
    public int EquipmentId { get; set; }
    public string TagNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Area { get; set; }
    public string DisciplineName { get; set; } = "";
    public bool HasDocuments { get; set; }
    public bool HasVdrItems { get; set; }
    public bool HasMR { get; set; }
    public bool HasWorkOrders { get; set; }
}

public class RelationshipGapReportDto
{
    public int TotalEquipmentCount { get; set; }
    public int FullyLinkedCount { get; set; }
    public int PartiallyLinkedCount { get; set; }
    public int UnlinkedCount { get; set; }
    public List<RelationshipGapItemDto> Items { get; set; } = new();
}

/// <summary>
/// طبق درخواست: تایید دسته‌جمعی پیشنهادهای اتصال در کل سیستم (نه یکی‌یکی) - چون تعداد
/// تجهیزات زیاد است. فقط شامل تطبیق‌های کامل (Exact Match روی شماره‌ی خام) است - همون سطح
/// اطمینانی که برای P&ID هم استفاده شد؛ تفاوت اینجاست که این‌جا نتیجه واقعاً یک FK دائمی
/// می‌سازد، پس هنوز نیاز به یک تایید صریح (هرچند دسته‌جمعی) دارد - نه خودکار بی‌صدا.
/// </summary>
public class BulkSuggestedLinkItemDto
{
    public int EquipmentId { get; set; }
    public string EquipmentTagNumber { get; set; } = "";
    public string TargetType { get; set; } = ""; // MR یا VDR
    public int TargetId { get; set; }
    public string TargetLabel { get; set; } = "";
    public string MatchedOnRawValue { get; set; } = "";
}
