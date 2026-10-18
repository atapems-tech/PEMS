namespace PEMS.Domain.Entities;

/// <summary>
/// وضعیت‌ها و جدول گذار مجاز StoreRequisition (درخواست داخلی کالا / Indent در PEMS). این جدول مرجع واحد است و در لایه‌ی
/// سرویس اعمال می‌شود (نه فقط در Controller): هیچ مسیر دیگری نمی‌تواند وضعیت را بپرد.
/// </summary>
public static class StoreRequisitionStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Reserved = "Reserved";
    public const string PartiallyIssued = "PartiallyIssued";
    public const string Issued = "Issued";
    public const string Closed = "Closed";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
        { Draft, Submitted, Approved, Reserved, PartiallyIssued, Issued, Closed, Rejected, Cancelled };

    private static readonly Dictionary<string, string[]> Allowed = new(StringComparer.Ordinal)
    {
        [Draft] = new[] { Submitted, Rejected, Cancelled },
        [Submitted] = new[] { Approved, Rejected, Cancelled },
        [Approved] = new[] { Reserved, Rejected, Cancelled },
        [Reserved] = new[] { PartiallyIssued, Issued, Cancelled, Closed },   // Closed از Reserved = بستن با کسریِ کامل (چیزی تحویل نشده؛ رزرو آزاد می‌شود)
        // تحویل بخشی می‌تواند تکرار شود؛ بستن با کسری (Closed) باقی‌مانده‌ی رزرو را آزاد می‌کند
        [PartiallyIssued] = new[] { PartiallyIssued, Issued, Closed },
        [Issued] = new[] { Closed },
        [Closed] = Array.Empty<string>(),
        [Rejected] = Array.Empty<string>(),
        [Cancelled] = Array.Empty<string>()
    };

    public static bool CanTransition(string from, string to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to, StringComparer.Ordinal);

    public static IReadOnlyList<string> NextOf(string from) =>
        Allowed.TryGetValue(from, out var next) ? next : Array.Empty<string>();

    public static bool IsTerminal(string status) => status is Closed or Rejected or Cancelled;
}

public static class MaterialReservationStatuses
{
    public const string Active = "Active";
    public const string PartiallyIssued = "PartiallyIssued";
    public const string FullyIssued = "FullyIssued";
    public const string Released = "Released";
}
