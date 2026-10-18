using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class ContractService : IContractService
{
    private readonly PEMSDbContext _context;

    public ContractService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<ContractDto>> GetAllAsync()
    {
        return await _context.Contracts
            .Include(x => x.Project)
            .Include(x => x.Vendor)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new ContractDto
            {
                ContractId = x.ContractId,
                ContractNo = x.ContractNo,
                ProjectId = x.ProjectId,
                ProjectName = x.Project!.ProjectName,
                VendorId = x.VendorId,
                VendorName = x.Vendor!.Name,
                ScopeOfWork = x.ScopeOfWork,
                ContractValue = x.ContractValue,
                Currency = x.Currency,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                Status = x.Status,
                PurchaseOrdersCount = x.PurchaseOrders.Count,
                RequiresInspection = x.RequiresInspection
            })
            .ToListAsync();
    }

    public async Task<ContractDto?> GetByIdAsync(int id)
    {
        var list = await GetAllAsync();
        return list.FirstOrDefault(x => x.ContractId == id);
    }

    // طبق «PEMS — Global Grid Hardening» P1: جایگزین GetAllAsync برای Index - شمارش/مرتب‌سازی/
    // صفحه‌بندی روی IQueryable قبل از Select. GetAllAsync/GetByIdAsync بالا دست‌نخورده ماندند.
    public async Task<PagedResult<ContractDto>> GetPagedAsync(int page, int pageSize)
    {
        var query = _context.Contracts.AsNoTracking().AsQueryable();

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.ContractId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ContractDto
            {
                ContractId = x.ContractId,
                ContractNo = x.ContractNo,
                ProjectId = x.ProjectId,
                ProjectName = x.Project!.ProjectName,
                VendorId = x.VendorId,
                VendorName = x.Vendor!.Name,
                ScopeOfWork = x.ScopeOfWork,
                ContractValue = x.ContractValue,
                Currency = x.Currency,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                Status = x.Status,
                PurchaseOrdersCount = x.PurchaseOrders.Count,
                RequiresInspection = x.RequiresInspection
            })
            .ToListAsync();

        return new PagedResult<ContractDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<int> CreateAsync(string contractNo, int projectId, int vendorId, string? scope,
        decimal? value, DateTime? startDate, DateTime? endDate, bool requiresInspection = false)
    {
        var contract = new Contract
        {
            ContractNo = contractNo,
            ProjectId = projectId,
            VendorId = vendorId,
            ScopeOfWork = scope,
            ContractValue = value,
            StartDate = startDate,
            EndDate = endDate,
            Status = "Draft",
            RequiresInspection = requiresInspection,
            CreatedDate = DateTime.Now
        };

        await _context.Contracts.AddAsync(contract);
        await _context.SaveChangesAsync();

        return contract.ContractId;
    }

    public async Task ChangeStatusAsync(int contractId, string status)
    {
        var contract = await _context.Contracts.FirstOrDefaultAsync(x => x.ContractId == contractId);
        if (contract == null) return;
        contract.Status = status;
        await _context.SaveChangesAsync();
    }
}
