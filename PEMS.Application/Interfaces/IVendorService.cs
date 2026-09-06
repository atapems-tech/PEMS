using PEMS.Application.DTOs;
using PEMS.Domain.Entities;

namespace PEMS.Application.Interfaces;

public interface IVendorService
{
    Task<List<VendorDto>> GetAllAsync();

    // طبق «PEMS — Global Grid Hardening» P1: GetAllAsync بالا کل جدول Vendors را بدون Skip/Take
    // بارگذاری می‌کند (GetByIdAsync هم از همین متد استفاده می‌کند - دست‌نخورده ماند). این متد
    // جدید و جدا صفحه‌بندی واقعی سمت SQL برای Index دارد.
    Task<PagedResult<VendorDto>> GetPagedAsync(int page, int pageSize);
    Task<VendorDto?> GetByIdAsync(int id);
    Task<Vendor?> GetEntityByIdAsync(int id);
    Task<int> CreateAsync(Vendor vendor);
    Task UpdateAsync(Vendor vendor);
    Task ToggleActiveAsync(int id);

    // پیدا کردن Vendor بر اساس نام (case-insensitive) یا ساخت خودکار آن - برای Import استفاده می‌شود
    Task<int> FindOrCreateByNameAsync(string name);
}
