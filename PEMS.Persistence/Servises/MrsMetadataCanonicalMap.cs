namespace PEMS.Persistence.Services;

/// <summary>
/// طبق سند «Material Master Import — Excel Metadata Extraction» بخش ۳-۴ و «Business Semantic
/// Mapping»: نگاشت متن سرستون واقعی Excel به یک نام معنایی پایدار (CanonicalField)، جدا از
/// FieldAliases موجود در MrsImportService (که برای پرکردن فیلدهای Typed قدیمی MrsImportRow
/// است، نه برای برچسب‌گذاری Metadata). هر سرستونی که این‌جا نگاشت ندارد، عمداً null می‌ماند -
/// یعنی هنوز به‌عنوان Metadata با OriginalHeader/Value ذخیره می‌شود، فقط CanonicalField ندارد
/// (نه حذف، نه حدس).
/// </summary>
public static class MrsMetadataCanonicalMap
{
    // تطبیق حساس به بزرگی/کوچکی حروف - فقط برای مواردی که خودِ فایل منبع دو ستون با نام
    // یکسان بعد از Normalize (بی‌تفاوت به حروف) دارد اما معنای متفاوتی مد نظر بوده
    // (مثال واقعی: "QTY check" در برابر "QTY CHECK" - هردو بعد از Normalize می‌شوند "qtycheck").
    private static readonly Dictionary<string, string> ExactCaseMap = new(StringComparer.Ordinal)
    {
        ["QTY check"] = "QtyCheck",
        ["QTY check2"] = "QtyCheck2",
        ["QTY CHECK"] = "CheckedQuantity",
    };

    private static readonly Dictionary<string, string> NormalizedMap = new(StringComparer.Ordinal)
    {
        ["logno"] = "LogNo",
        ["coderahkaran"] = "CodeRahkaran",
        ["packno"] = "PackNumber",
        ["packingno"] = "PackNumber",
        ["pono"] = "PurchaseOrderNumber",
        ["p/ono"] = "PurchaseOrderNumber",
        // طبق سند بخش ۳: "PO No"/"Purchase_Number" بعد از Normalize همان "pono" بالا می‌شوند؛
        // این دو مورد اضافه هستند چون به‌شکل دیگری Normalize می‌شوند
        ["purchasenumber"] = "PurchaseOrderNumber",
        ["ponumber"] = "PurchaseOrderNumber",
        ["purchaseno"] = "PurchaseOrderNumber",
        ["mrsno"] = "MRSNumber",
        ["typeofmaterial"] = "TypeOfMaterial",
        ["vendor"] = "VendorName",
        ["vendorname"] = "VendorName",
        ["supplier"] = "VendorName",
        ["suppliername"] = "VendorName",
        ["descriptionpackinglist"] = "Description",
        ["description"] = "Description",
        ["چک"] = "Check",
        ["size1"] = "Size1",
        ["size2"] = "Size2",
        ["size3"] = "Size3",
        ["qty"] = "Quantity",
        ["qtymiv"] = "MIVQuantity",
        ["qtymrv"] = "MRVQuantity",
        ["qtyissue"] = "IssuedQuantity",
        ["qtyissued"] = "IssuedQuantity",
        ["qtyremain"] = "RemainingQuantity",
        ["unit"] = "Unit",
        ["unt"] = "Unit",
        ["uom"] = "Unit",
        ["location"] = "StorageLocation",
        ["mivno"] = "MIVNumber",
        ["receivedby"] = "ReceivedBy",
        ["mrvno"] = "MRVNumber",
        ["colour"] = "Colour",
        ["color"] = "Colour",
        ["drumno"] = "DrumNumber",
        ["checkmark"] = "CheckMark",
        ["heat/tag/serno"] = "HeatTagSerial",
        ["tag"] = "HeatTagSerial",
        ["wight"] = "Weight",
        ["weight"] = "Weight",
        ["daterecived"] = "ReceivedDate",
        ["datereceived"] = "ReceivedDate",
        ["checkdate"] = "CheckDate",
        ["remark"] = "Remark",
        ["note"] = "Note",
    };

    /// <summary>
    /// originalHeader دقیقاً همان متن Trim‌شده‌ی سلول سرستون (بدون تغییر بزرگی/کوچکی حروف) است.
    /// </summary>
    public static string? Resolve(string originalHeader)
    {
        if (string.IsNullOrWhiteSpace(originalHeader)) return null;

        if (ExactCaseMap.TryGetValue(originalHeader.Trim(), out var exact))
            return exact;

        var normalized = Normalize(originalHeader);
        return NormalizedMap.TryGetValue(normalized, out var mapped) ? mapped : null;
    }

    private static string Normalize(string raw)
    {
        return raw.Trim().ToLowerInvariant()
            .Replace(" ", "").Replace(".", "").Replace("\n", "").Replace("\r", "")
            .Replace("_", "").Replace("(", "").Replace(")", "").Replace(":", "");
    }
}
