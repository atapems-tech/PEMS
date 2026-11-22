using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-718: the lists of the new MDB form.</summary>
public sealed class MdbOptionsOperations(PEMSDbContext context) : IMdbOptionsOperations
{
    public Task<List<QcOption>> ProjectsAsync()
        => context.Projects.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.ProjectName).Select(x => new QcOption(x.ProjectId.ToString(), x.ProjectName)).ToListAsync();

    public Task<List<QcOption>> VendorsAsync()
        => context.Vendors.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new QcOption(x.VendorId.ToString(), x.Name)).ToListAsync();

    public Task<List<QcOption>> EquipmentAsync()
        => context.Equipments.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.TagNumber).Select(x => new QcOption(x.EquipmentId.ToString(), x.TagNumber)).ToListAsync();

    public Task<List<QcOption>> PurchaseOrdersAsync()
        => context.PurchaseOrders.AsNoTracking().OrderBy(x => x.PoNumber).Select(x => new QcOption(x.PoId.ToString(), x.PoNumber)).ToListAsync();
}
