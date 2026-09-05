using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// کاتالوگ قطعات یدکی + موجودی انبار - طبق سند «PEMS — Global Data Grid Standard»: صفحه‌بندی/
/// فیلتر/مرتب‌سازی همه در SQL.
/// </summary>
public interface ISparePartService
{
    Task<PagedResult<SparePartListDto>> GetListAsync(int page = 1, int pageSize = 50, string? q = null, bool lowStockOnly = false);

    /// <summary>
    /// دقیقاً همون رفتار قبلی SparePartController.Index: این شمارنده روی کل قطعات فعال حساب
    /// می‌شود، نه محدود به فیلتر q/lowStockOnly فعلی صفحه.
    /// </summary>
    Task<int> GetLowStockCountAsync();
}
