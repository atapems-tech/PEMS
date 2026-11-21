using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// گام و مرحله عملیاتی در مسیر فرآیند تولید (Process Route Operation).
/// زمان راه‌اندازی (Setup)، زمان اجرای استاندارد به ازای هر بچ، ایستگاه کاری و پارامترهای فنی را نگهداری می‌کند.
/// </summary>
public class ProcessRouteOperation : BaseEntity
{
    public int ProcessRouteOperationId { get; set; }

    public int ProcessRouteId { get; set; }
    public ProcessRoute? ProcessRoute { get; set; }

    public int OperationSequence { get; set; } = 10;

    public string OperationName { get; set; } = string.Empty;

    public int ProductionWorkCenterId { get; set; }
    public ProductionWorkCenter? ProductionWorkCenter { get; set; }

    public decimal SetupTimeHours { get; set; } = 0m;

    public decimal RunTimeHoursPerBatch { get; set; }

    public int? RequiredCrewSize { get; set; }

    // مقادیر فنی مانند دما، فشار، سرعت اختلاط و ... در قالب ساختار منعطف JSON
    public string? OperatingParametersJson { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
