using PEMS.Application.Inventory;

namespace PEMS.Application.Interfaces;

/// <summary>
/// Phase 2 / P1 - موجودی قابل‌استفاده‌ی هر (متریال، انبار)، فقط‌خواندنی: هیچ نوشتن، رزرو، تراکنش، تغییر وضعیت یا اصلاح خودکاری ندارد.
/// UsableStock = OnHand - Reserved (همان AvailableQuantity سرویس‌های موجود). مقدار منفی/نامعتبر Clamp یا اصلاح نمی‌شود؛ گزارش می‌شود.
/// </summary>
public interface IUsableStockService
{
    Task<IReadOnlyList<UsableStockResult>> GetUsableStockAsync(IReadOnlyCollection<StockKey> keys, CancellationToken ct = default);
}

/// <summary>محدوده‌ی جمع‌آوری؛ null = همه. فیلتر فقط محدودکننده است و هرگز منبع تازه نمی‌سازد.</summary>
public sealed record NetDemandScope(IReadOnlyCollection<int>? MaterialItemIds = null, IReadOnlyCollection<int>? WarehouseIds = null);

public sealed record DemandCollection(IReadOnlyList<DemandLine> Lines, IReadOnlyList<ExcludedDemand> Excluded);

/// <summary>Phase 2 / P3 - جمع‌آوری تقاضای تأییدشده از منابع دارای ارتباط/وضعیت/مقدار قابل‌اتکا (SR line و WorkOrderMaterial). فقط‌خواندنی.</summary>
public interface INetDemandCollector
{
    Task<DemandCollection> CollectAsync(NetDemandScope scope, CancellationToken ct = default);
}

public sealed record NetDemandCaller(IReadOnlyList<string> Roles);

public sealed record NetDemandReport(bool Authorized, IReadOnlyList<NetDemandResult> Results);

/// <summary>
/// ترکیب‌کننده‌ی نازک: مجوز → جمع‌آوری → موجودی → محاسبه‌ی خالص → پوشاندن هویت منابع غیرقابل‌دسترس. هیچ توصیه/ذخیره/خرید/رزروی ندارد.
/// </summary>
public interface INetDemandService
{
    Task<NetDemandReport> CalculateAsync(NetDemandScope scope, NetDemandCaller caller, CancellationToken ct = default);

    /// <summary>
    /// Phase 2 / P5 - محاسبه‌ی بدون مجوز و بدون پوشاندن هویت منابع، فقط برای سرویس‌های سیستمی که خودشان مجوز اقدام‌کننده را بررسی می‌کنند
    /// (ذخیره‌ی توصیه به شناسه‌ی واقعی منبع نیاز دارد). هرگز مستقیم به Controller/View داده نشود.
    /// </summary>
    Task<IReadOnlyList<NetDemandResult>> CalculateUnmaskedForSystemAsync(NetDemandScope scope, CancellationToken ct = default);
}
