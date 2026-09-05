using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly PEMSDbContext _context;

    public PurchaseOrderService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<PurchaseOrderDto>> GetAllAsync()
    {
        return await _context.PurchaseOrders
            .Include(x => x.Project)
            .Include(x => x.Vendor)
            .Include(x => x.Contract)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new PurchaseOrderDto
            {
                PoId = x.PoId,
                PoNumber = x.PoNumber,
                ProjectId = x.ProjectId,
                ProjectName = x.Project!.ProjectName,
                VendorId = x.VendorId,
                VendorName = x.Vendor!.Name,
                ContractId = x.ContractId,
                ContractNo = x.Contract != null ? x.Contract.ContractNo : null,
                Description = x.Description,
                Value = x.Value,
                Status = x.Status,
                VDRItemsCount = x.VDRItems.Count
            })
            .ToListAsync();
    }

    public async Task<PurchaseOrderDto?> GetByIdAsync(int id)
    {
        var list = await GetAllAsync();
        return list.FirstOrDefault(x => x.PoId == id);
    }

    public async Task<int> CreateAsync(string poNumber, int projectId, int vendorId, int? contractId,
        string? description, decimal? value)
    {
        var po = new PurchaseOrder
        {
            PoNumber = poNumber,
            ProjectId = projectId,
            VendorId = vendorId,
            ContractId = contractId,
            Description = description,
            Value = value,
            Status = "Issued",
            CreatedDate = DateTime.Now
        };

        await _context.PurchaseOrders.AddAsync(po);
        await _context.SaveChangesAsync();

        return po.PoId;
    }

    public async Task<int> FindOrCreateByNumberAsync(string poNumber, int projectId, int vendorId)
    {
        var trimmed = poNumber.Trim();

        var existing = await _context.PurchaseOrders
            .FirstOrDefaultAsync(x => x.PoNumber.ToLower() == trimmed.ToLower() && x.ProjectId == projectId);

        if (existing != null)
            return existing.PoId;

        var po = new PurchaseOrder
        {
            PoNumber = trimmed,
            ProjectId = projectId,
            VendorId = vendorId,
            Status = "Issued",
            CreatedDate = DateTime.Now
        };

        await _context.PurchaseOrders.AddAsync(po);
        await _context.SaveChangesAsync();

        return po.PoId;
    }
}
