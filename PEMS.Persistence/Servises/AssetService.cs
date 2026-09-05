using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق PEMS_CMMS_ARCHITECTURE.md بخش ۴: چرخه‌عمر CMMS یک Equipment - نگهداری، بازرسی،
/// خرابی و هزینه. اهمیت (Criticality) همیشه از EquipmentCriticality واقعی خوانده می‌شود،
/// هرگز روی خودِ Asset ذخیره نمی‌شود، تا دو منبع حقیقت نداشته باشیم.
/// </summary>
public class AssetService : IAssetService
{
    private readonly PEMSDbContext _context;
    private readonly IFunctionalLocationService _functionalLocationService;

    public AssetService(PEMSDbContext context, IFunctionalLocationService functionalLocationService)
    {
        _context = context;
        _functionalLocationService = functionalLocationService;
    }

    public async Task<PagedResult<AssetListDto>> GetListAsync(int page = 1, int pageSize = 50, string? status = null, string? q = null)
    {
        var query = _context.Assets
            .Include(x => x.Equipment)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(x => x.AssetNumber.ToLower().Contains(term)
                || (x.Equipment != null && x.Equipment.TagNumber.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync();

        page = page < 1 ? 1 : page;
        // Defense-in-depth طبق سند Global Data Grid Standard - حتی اگر Controller قبلاً
        // Normalize کرده باشد، Service هم دوباره فقط ۲۰/۵۰/۱۰۰ را می‌پذیرد.
        pageSize = DataGridPageSize.Normalize(pageSize);

        var assets = await query
            .OrderBy(x => x.AssetNumber)
            .ThenBy(x => x.AssetId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = new List<AssetListDto>();
        foreach (var a in assets)
        {
            items.Add(new AssetListDto
            {
                AssetId = a.AssetId,
                AssetNumber = a.AssetNumber,
                EquipmentId = a.EquipmentId,
                EquipmentTagNumber = a.Equipment?.TagNumber ?? "",
                EquipmentName = a.Equipment?.Name ?? "",
                MaintenanceClass = a.MaintenanceClass,
                Status = a.Status,
                CommissioningDate = a.CommissioningDate,
                CriticalityClass = await _context.EquipmentCriticalities
                    .Where(x => x.EquipmentId == a.EquipmentId && x.IsActive)
                    .Select(x => x.CriticalityClass)
                    .FirstOrDefaultAsync(),
                OpenWorkOrderCount = await _context.WorkOrders
                    .CountAsync(w => w.EquipmentId == a.EquipmentId && w.IsActive && w.Status != "Completed" && w.Status != "Closed"),
                FunctionalLocationId = a.FunctionalLocationId,
                FunctionalLocationBreadcrumb = await _functionalLocationService.GetBreadcrumbAsync(a.FunctionalLocationId)
            });
        }

        return new PagedResult<AssetListDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<AssetDetailDto?> GetDetailsAsync(int assetId)
    {
        var asset = await _context.Assets.Include(x => x.Equipment).FirstOrDefaultAsync(x => x.AssetId == assetId);
        if (asset == null) return null;
        return await BuildDetailAsync(asset);
    }

    public async Task<AssetSummaryDto?> GetByEquipmentIdAsync(int equipmentId)
    {
        var asset = await _context.Assets.FirstOrDefaultAsync(x => x.EquipmentId == equipmentId && x.IsActive);
        if (asset == null) return null;

        return new AssetSummaryDto
        {
            AssetId = asset.AssetId,
            AssetNumber = asset.AssetNumber,
            FunctionalLocation = asset.FunctionalLocation,
            FunctionalLocationBreadcrumb = await _functionalLocationService.GetBreadcrumbAsync(asset.FunctionalLocationId),
            MaintenanceClass = asset.MaintenanceClass,
            CommissioningDate = asset.CommissioningDate,
            Status = asset.Status,
            CriticalityClass = await _context.EquipmentCriticalities
                .Where(x => x.EquipmentId == equipmentId && x.IsActive)
                .Select(x => x.CriticalityClass)
                .FirstOrDefaultAsync()
        };
    }

    public async Task<(bool Success, string? Error, int? AssetId)> CreateAsync(
        int equipmentId, string assetNumber, string? functionalLocation, string? maintenanceClass,
        DateTime? commissioningDate, string status, string? description, string? currentUserName,
        int? functionalLocationId = null)
    {
        assetNumber = assetNumber?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(assetNumber)) return (false, "شماره Asset الزامی است.", null);

        if (!await _context.Equipments.AnyAsync(x => x.EquipmentId == equipmentId && x.IsActive))
            return (false, "تجهیز انتخاب‌شده نامعتبره.", null);

        if (await _context.Assets.AnyAsync(x => x.EquipmentId == equipmentId && x.IsActive))
            return (false, "این تجهیز از قبل یک Asset دارد.", null);

        if (await _context.Assets.AnyAsync(x => x.AssetNumber == assetNumber && x.IsActive))
            return (false, $"شماره Asset «{assetNumber}» قبلاً استفاده شده است.", null);

        var asset = new Asset
        {
            EquipmentId = equipmentId,
            AssetNumber = assetNumber,
            FunctionalLocation = functionalLocation,
            FunctionalLocationId = functionalLocationId,
            MaintenanceClass = maintenanceClass,
            CommissioningDate = commissioningDate,
            Status = string.IsNullOrWhiteSpace(status) ? "Active" : status,
            Description = description,
            IsActive = true,
            CreatedBy = currentUserName
        };

        await _context.Assets.AddAsync(asset);
        await _context.SaveChangesAsync();

        return (true, null, asset.AssetId);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(
        int assetId, string assetNumber, string? functionalLocation, string? maintenanceClass,
        DateTime? commissioningDate, string status, string? description, string? currentUserName,
        int? functionalLocationId = null)
    {
        var asset = await _context.Assets.FirstOrDefaultAsync(x => x.AssetId == assetId);
        if (asset == null) return (false, "Asset پیدا نشد.");

        assetNumber = assetNumber?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(assetNumber)) return (false, "شماره Asset الزامی است.");

        if (await _context.Assets.AnyAsync(x => x.AssetId != assetId && x.AssetNumber == assetNumber && x.IsActive))
            return (false, $"شماره Asset «{assetNumber}» قبلاً استفاده شده است.");

        asset.AssetNumber = assetNumber;
        asset.FunctionalLocation = functionalLocation;
        asset.FunctionalLocationId = functionalLocationId;
        asset.MaintenanceClass = maintenanceClass;
        asset.CommissioningDate = commissioningDate;
        asset.Status = string.IsNullOrWhiteSpace(status) ? asset.Status : status;
        asset.Description = description;
        asset.ModifiedBy = currentUserName;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task ToggleActiveAsync(int assetId, string? currentUserName)
    {
        var asset = await _context.Assets.FirstOrDefaultAsync(x => x.AssetId == assetId);
        if (asset == null) return;

        asset.IsActive = !asset.IsActive;
        asset.ModifiedBy = currentUserName;
        await _context.SaveChangesAsync();
    }

    private async Task<AssetDetailDto> BuildDetailAsync(Asset asset)
    {
        var criticality = await _context.EquipmentCriticalities
            .Where(x => x.EquipmentId == asset.EquipmentId && x.IsActive)
            .Select(x => new { x.CriticalityClass, x.RiskLevel })
            .FirstOrDefaultAsync();

        var workOrders = await _context.WorkOrders
            .Where(x => x.EquipmentId == asset.EquipmentId)
            .OrderByDescending(x => x.CreatedDate)
            .Select(w => new WorkOrderSummaryDto
            {
                WorkOrderId = w.WorkOrderId,
                WorkOrderNo = w.WorkOrderNumber,
                Description = w.Description,
                Status = w.Status,
                CreatedDate = w.CreatedDate
            })
            .ToListAsync();

        var inspections = await _context.InspectionRecords
            .Where(x => x.EquipmentId == asset.EquipmentId && x.IsActive)
            .OrderByDescending(x => x.InspectionDate)
            .Select(i => new InspectionSummaryDto
            {
                InspectionRecordId = i.InspectionRecordId,
                InspectionType = i.InspectionType,
                InspectionDate = i.InspectionDate,
                Inspector = i.Inspector,
                Result = i.Result
            })
            .ToListAsync();

        var failures = await _context.FailureRecords
            .Where(x => x.EquipmentId == asset.EquipmentId && x.IsActive)
            .OrderByDescending(x => x.FailureDate)
            .Select(f => new FailureSummaryDto
            {
                FailureRecordId = f.FailureRecordId,
                FailureDate = f.FailureDate,
                FailureMode = f.FailureMode,
                FailureCause = f.FailureCause,
                DowntimeHours = f.DowntimeHours,
                WorkOrderId = f.WorkOrderId
            })
            .ToListAsync();

        var latestMaintenance = await _context.MaintenanceHistories
            .Where(x => x.EquipmentId == asset.EquipmentId && x.IsActive)
            .OrderByDescending(x => x.ExecutionDate)
            .Select(m => new MaintenanceHistorySummaryDto
            {
                ExecutionDate = m.ExecutionDate,
                ActionTaken = m.ActionTaken,
                Technician = m.Technician,
                DurationHours = m.DurationHours,
                Cost = m.Cost
            })
            .FirstOrDefaultAsync();

        var workOrderIds = workOrders.Select(w => w.WorkOrderId).ToList();
        var laborCost = await _context.WorkOrderLabors
            .Where(x => workOrderIds.Contains(x.WorkOrderId) && x.IsActive)
            .SumAsync(x => (decimal?)x.LaborCost) ?? 0;
        var spareCost = await _context.MaintenanceSpareUsages
            .Where(x => workOrderIds.Contains(x.WorkOrderId) && x.IsActive)
            .SumAsync(x => (decimal?)x.TotalCost) ?? 0;

        return new AssetDetailDto
        {
            AssetId = asset.AssetId,
            AssetNumber = asset.AssetNumber,
            EquipmentId = asset.EquipmentId,
            EquipmentTagNumber = asset.Equipment?.TagNumber ?? "",
            EquipmentName = asset.Equipment?.Name ?? "",
            Manufacturer = asset.Equipment?.Manufacturer,
            Model = asset.Equipment?.Model,
            FunctionalLocation = asset.FunctionalLocation,
            FunctionalLocationId = asset.FunctionalLocationId,
            FunctionalLocationBreadcrumb = await _functionalLocationService.GetBreadcrumbAsync(asset.FunctionalLocationId),
            MaintenanceClass = asset.MaintenanceClass,
            CommissioningDate = asset.CommissioningDate,
            Status = asset.Status,
            Description = asset.Description,
            CriticalityClass = criticality?.CriticalityClass,
            RiskLevel = criticality?.RiskLevel,
            IsActive = asset.IsActive,
            WorkOrders = workOrders,
            Inspections = inspections,
            Failures = failures,
            LatestMaintenance = latestMaintenance,
            TotalCost = laborCost + spareCost
        };
    }
}
