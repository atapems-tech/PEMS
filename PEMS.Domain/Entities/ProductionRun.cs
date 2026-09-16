using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» بخش U/W: «چه اتفاقی واقعاً افتاد» -
/// جدا از ProductionOrder («چه چیزی باید تولید شود»). مقدار واقعی تولیدشده عمداً این‌جا
/// ذخیره/نگهداری نمی‌شود - از مجموع ProductionOutput های همین Run مشتق می‌شود (مثل الگوی
/// MaterialStock.OnHandQuantity که فقط از طریق Service نگهداری می‌شود، نه یک فیلد مستقل دوم
/// که ممکنه با واقعیت ناهماهنگ شود).
/// </summary>
public class ProductionRun : BaseEntity
{
    public int ProductionRunId { get; set; }

    public int ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    public string RunNumber { get; set; } = string.Empty;

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    // Planned, InProgress, Paused, OnHold, Completed, Cancelled
    public string Status { get; set; } = "Planned";

    public string? PreHoldStatus { get; set; }

    // اختیاری - اگر خالی باشد از ProductionOrder.ProductionWorkCenterId استفاده می‌شود
    public int? ProductionWorkCenterId { get; set; }
    public ProductionWorkCenter? ProductionWorkCenter { get; set; }

    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public int? OperatorEmployeeId { get; set; }
    public Employee? OperatorEmployee { get; set; }

    public int? ShiftDefinitionId { get; set; }
    public ShiftDefinition? ShiftDefinition { get; set; }

    public string? Notes { get; set; }
    public string? HoldReason { get; set; }
    public string? CancellationReason { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<ProductionInput> Inputs { get; set; } = new List<ProductionInput>();
    public ICollection<ProductionOutput> Outputs { get; set; } = new List<ProductionOutput>();
    public ICollection<ProductionLoss> Losses { get; set; } = new List<ProductionLoss>();
    public ICollection<ProductionEvent> Events { get; set; } = new List<ProductionEvent>();
}
