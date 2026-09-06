using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class VendorService : IVendorService
{
    private readonly PEMSDbContext _context;

    public VendorService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<VendorDto>> GetAllAsync()
    {
        return await _context.Vendors
            .OrderBy(x => x.Name)
            .Select(x => new VendorDto
            {
                VendorId = x.VendorId,
                Name = x.Name,
                VendorCode = x.VendorCode,
                Country = x.Country,
                ContactName = x.ContactName,
                ContactEmail = x.ContactEmail,
                ContactPhone = x.ContactPhone,
                RiskLevel = x.RiskLevel,
                RiskScore = x.RiskScore,
                RiskAssessedDate = x.RiskAssessedDate,
                IsActive = x.IsActive,
                ContractsCount = x.Contracts.Count,
                VDRItemsCount = x.VDRItems.Count,
                OverdueVDRItemsCount = x.VDRItems.Count(v =>
                    v.WorkflowStatus != "Closed" &&
                    v.PlannedFirstIssueDate != null &&
                    v.PlannedFirstIssueDate < DateTime.Now &&
                    !v.Submissions.Any())
            })
            .ToListAsync();
    }

    public async Task<VendorDto?> GetByIdAsync(int id)
    {
        var list = await GetAllAsync();
        return list.FirstOrDefault(x => x.VendorId == id);
    }

    // طبق «PEMS — Global Grid Hardening» P1: جایگزین GetAllAsync برای Index - شمارش/مرتب‌سازی/
    // صفحه‌بندی روی IQueryable قبل از Select. GetAllAsync/GetByIdAsync بالا دست‌نخورده ماندند
    // (عمداً فیلتر IsActive اعمال نشده - همان رفتار فعلی GetAllAsync که Vendor غیرفعال را هم
    // نشان می‌دهد، حفظ شد).
    public async Task<PagedResult<VendorDto>> GetPagedAsync(int page, int pageSize)
    {
        var query = _context.Vendors.AsNoTracking().AsQueryable();

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.VendorId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new VendorDto
            {
                VendorId = x.VendorId,
                Name = x.Name,
                VendorCode = x.VendorCode,
                Country = x.Country,
                ContactName = x.ContactName,
                ContactEmail = x.ContactEmail,
                ContactPhone = x.ContactPhone,
                RiskLevel = x.RiskLevel,
                RiskScore = x.RiskScore,
                RiskAssessedDate = x.RiskAssessedDate,
                IsActive = x.IsActive,
                ContractsCount = x.Contracts.Count,
                VDRItemsCount = x.VDRItems.Count,
                OverdueVDRItemsCount = x.VDRItems.Count(v =>
                    v.WorkflowStatus != "Closed" &&
                    v.PlannedFirstIssueDate != null &&
                    v.PlannedFirstIssueDate < DateTime.Now &&
                    !v.Submissions.Any())
            })
            .ToListAsync();

        return new PagedResult<VendorDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<Vendor?> GetEntityByIdAsync(int id)
    {
        return await _context.Vendors.FirstOrDefaultAsync(x => x.VendorId == id);
    }

    public async Task<int> CreateAsync(Vendor vendor)
    {
        vendor.CreatedDate = DateTime.Now;
        await _context.Vendors.AddAsync(vendor);
        await _context.SaveChangesAsync();
        return vendor.VendorId;
    }

    public async Task UpdateAsync(Vendor vendor)
    {
        // همان رفع باگ RowVersion که در EngineeringDocumentRepository اعمال شد: رکورد ردیابی‌شده
        // را می‌خوانیم و فقط فیلدهای قابل‌ویرایش را کپی می‌کنیم، نه Update() روی شیء جداشده‌ی فرم.
        var existing = await _context.Vendors.FirstOrDefaultAsync(x => x.VendorId == vendor.VendorId);
        if (existing == null) return;

        existing.Name = vendor.Name;
        existing.VendorCode = vendor.VendorCode;
        existing.Country = vendor.Country;
        existing.ContactName = vendor.ContactName;
        existing.ContactEmail = vendor.ContactEmail;
        existing.ContactPhone = vendor.ContactPhone;

        await _context.SaveChangesAsync();
    }

    public async Task ToggleActiveAsync(int id)
    {
        var vendor = await _context.Vendors.FirstOrDefaultAsync(x => x.VendorId == id);
        if (vendor == null) return;
        vendor.IsActive = !vendor.IsActive;
        await _context.SaveChangesAsync();
    }

    public async Task<int> FindOrCreateByNameAsync(string name)
    {
        var trimmed = name.Trim();

        var existing = await _context.Vendors
            .FirstOrDefaultAsync(x => x.Name.ToLower() == trimmed.ToLower());

        if (existing != null)
            return existing.VendorId;

        var vendor = new Vendor
        {
            Name = trimmed,
            RiskLevel = "Unknown",
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.Vendors.AddAsync(vendor);
        await _context.SaveChangesAsync();

        return vendor.VendorId;
    }
}
