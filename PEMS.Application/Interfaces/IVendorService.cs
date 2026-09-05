using PEMS.Application.DTOs;
using PEMS.Domain.Entities;

namespace PEMS.Application.Interfaces;

public interface IVendorService
{
    Task<List<VendorDto>> GetAllAsync();
    Task<VendorDto?> GetByIdAsync(int id);
    Task<Vendor?> GetEntityByIdAsync(int id);
    Task<int> CreateAsync(Vendor vendor);
    Task UpdateAsync(Vendor vendor);
    Task ToggleActiveAsync(int id);

    // پیدا کردن Vendor بر اساس نام (case-insensitive) یا ساخت خودکار آن - برای Import استفاده می‌شود
    Task<int> FindOrCreateByNameAsync(string name);
}
