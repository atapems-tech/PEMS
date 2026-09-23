namespace PEMS.Application.DTOs;

/// <summary>
/// گزارش تدارکات (MR/MRQ/Procurement Package) - طبق درخواست: هر بخش گزارش‌گیری مخصوص خودش.
/// </summary>
public class ProcurementReportDto
{
    public int TotalPackages { get; set; }
    public int TotalMRs { get; set; }
    public int TotalMRQs { get; set; }
    public int OpenCount { get; set; }
    public int ClosedCount { get; set; }

    public List<(string Month, int Count)> VolumeByMonth { get; set; } = new();
    public List<(string Status, int Count)> ByStatus { get; set; } = new();
    public List<(string Vendor, int Count)> ByVendor { get; set; } = new();
    public List<(string Discipline, int Count)> ByDiscipline { get; set; } = new();

    // ==============================================================================
    // طبق گزارش سخت‌افزاری نهایی Package Procurement (Gap ۵): زنجیره‌ی Tender/Bid/Contract/PO/NTP
    // پیش از این جلسه اصلاً وجود نداشت، پس هیچ‌جای این گزارش قابل مشاهده نبود - افزودنی خالص،
    // هیچ فیلد/متد موجودی تغییر نکرده.
    // ==============================================================================
    public int TotalTenders { get; set; }
    public int TotalBids { get; set; }
    public int TotalContracts { get; set; }
    public int TotalPurchaseOrders { get; set; }
    public int TotalNTPsIssued { get; set; }
    public List<(string Status, int Count)> ByContractStatus { get; set; } = new();
}

/// <summary>
/// یک ردیف زنجیره‌ی کامل Tender→Winner→Contract→PO→NTP→Kick-off - طبق Gap ۵. صفحه‌بندی‌شده
/// (نه در ProcurementReportDto اصلی) تا داشبورد خلاصه سنگین نشود.
/// </summary>
public class ProcurementPipelineRowDto
{
    public string PackageNumber { get; set; } = "";
    public string TenderNumber { get; set; } = "";
    public string TenderStatus { get; set; } = "";
    public string? WinnerVendorName { get; set; }
    public string? BidStatus { get; set; }
    public string? TechnicalEvaluationStatus { get; set; }
    public string? CommercialEvaluationStatus { get; set; }
    public string? ContractNo { get; set; }
    public string? ContractStatus { get; set; }
    public decimal? ContractValue { get; set; }
    public string? PoNumber { get; set; }
    public string? NtpStatus { get; set; }
    public string? KickoffStatus { get; set; }
}
