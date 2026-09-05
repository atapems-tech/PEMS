using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق سند Inventory بخش ۵-۶: MaterialItem = تعریف متریال (Master)، جدا از موجودی واقعی
/// (MaterialStock) - این سرویس فقط کاتالوگ را مدیریت می‌کند، موجودی از MaterialStockService می‌آید.
/// </summary>
public class MaterialItemService : IMaterialItemService
{
    private readonly PEMSDbContext _context;

    public MaterialItemService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<MaterialItemListDto>> GetListAsync(
        int page = 1, int pageSize = 25, string? q = null, int? categoryId = null,
        bool lowStockOnly = false, bool criticalOnly = false)
    {
        var query = _context.MaterialItems
            .Include(x => x.Category)
            .Include(x => x.Discipline)
            .Include(x => x.Uom)
            .Include(x => x.Vendor)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(x => x.MaterialCode.ToLower().Contains(term) || x.Description.ToLower().Contains(term));
        }

        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId);
        if (criticalOnly) query = query.Where(x => x.IsCritical);

        // طبق رفع باگ فاز ۶B (P0): قبلاً lowStockOnly بعد از CountAsync/Skip/Take روی همون
        // صفحه‌ی نتیجه در حافظه اعمال می‌شد - یعنی TotalCount/TotalPages روی مجموعه‌ی
        // فیلترنشده محاسبه می‌شد و آیتم‌های کم‌موجودی صفحات بعدی هرگز دیده نمی‌شدند (یک صفحه
        // می‌توانست کمتر از pageSize یا حتی صفر ردیف برگرداند در حالی که آیتم‌های بیشتری در
        // صفحات دیگر وجود داشت). حالا دقیقاً همون فرمول LowStock (GroupBy پایین‌تر) به‌شکل یک
        // EXISTS Subquery روی IQueryable اعمال می‌شود - قبل از CountAsync/Skip/Take، نه بعدش.
        if (lowStockOnly)
        {
            query = query.Where(x => _context.MaterialStocks.Any(s =>
                s.MaterialItemId == x.MaterialItemId && s.IsActive &&
                s.ReorderPoint != null && (s.OnHandQuantity - s.ReservedQuantity) <= s.ReorderPoint));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(x => x.MaterialCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var itemIds = items.Select(x => x.MaterialItemId).ToList();
        // توجه: IsLowStock/AvailableQuantity روی MaterialStock عمداً [NotMapped] هستند (طبق
        // بخش ۹ سند) - این‌جا باید معادل SQL-ترجمه‌پذیرشون مستقیم نوشته بشه، وگرنه EF Core
        // این Query را ترجمه نمی‌کند و کل صفحه‌ی Material Master با خطا مواجه می‌شود.
        var stockTotals = await _context.MaterialStocks
            .Where(x => itemIds.Contains(x.MaterialItemId) && x.IsActive)
            .GroupBy(x => x.MaterialItemId)
            .Select(g => new
            {
                MaterialItemId = g.Key,
                OnHand = g.Sum(x => x.OnHandQuantity),
                Reserved = g.Sum(x => x.ReservedQuantity),
                LowStock = g.Any(x => x.ReorderPoint != null && (x.OnHandQuantity - x.ReservedQuantity) <= x.ReorderPoint)
            })
            .ToListAsync();

        var result = items.Select(x =>
        {
            var stock = stockTotals.FirstOrDefault(s => s.MaterialItemId == x.MaterialItemId);
            return new MaterialItemListDto
            {
                MaterialItemId = x.MaterialItemId,
                MaterialCode = x.MaterialCode,
                Description = x.Description,
                CategoryName = x.Category?.Name,
                DisciplineName = x.Discipline?.Name,
                UomCode = x.Uom?.Code,
                Manufacturer = x.Manufacturer,
                VendorName = x.Vendor?.Name,
                IsStockItem = x.IsStockItem,
                IsCritical = x.IsCritical,
                IsActive = x.IsActive,
                TotalOnHand = stock?.OnHand ?? 0,
                TotalAvailable = (stock?.OnHand ?? 0) - (stock?.Reserved ?? 0),
                IsLowStock = stock?.LowStock ?? false
            };
        }).ToList();

        return new PagedResult<MaterialItemListDto> { Items = result, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<MaterialItemDetailDto?> GetDetailsAsync(int materialItemId)
    {
        var item = await _context.MaterialItems
            .Include(x => x.Category).Include(x => x.Discipline).Include(x => x.Uom).Include(x => x.Vendor)
            .FirstOrDefaultAsync(x => x.MaterialItemId == materialItemId);

        if (item == null) return null;

        // طبق همون قاعده‌ی [NotMapped]: AvailableQuantity/IsLowStock/IsOutOfStock باید بعد از
        // ToListAsync در حافظه محاسبه بشن، نه داخل Select ای که به SQL ترجمه می‌شه.
        var stockEntities = await _context.MaterialStocks
            .Include(x => x.Warehouse).Include(x => x.StorageLocation)
            .Where(x => x.MaterialItemId == materialItemId && x.IsActive)
            .ToListAsync();

        var stocks = stockEntities.Select(x => new MaterialStockRowDto
            {
                MaterialStockId = x.MaterialStockId,
                WarehouseId = x.WarehouseId,
                WarehouseCode = x.Warehouse != null ? x.Warehouse.Code : "",
                WarehouseName = x.Warehouse != null ? x.Warehouse.Name : "",
                StorageLocationCode = x.StorageLocation != null ? x.StorageLocation.Code : null,
                OnHandQuantity = x.OnHandQuantity,
                ReservedQuantity = x.ReservedQuantity,
                AvailableQuantity = x.AvailableQuantity,
                MinimumQuantity = x.MinimumQuantity,
                MaximumQuantity = x.MaximumQuantity,
                ReorderPoint = x.ReorderPoint,
                IsLowStock = x.IsLowStock,
                IsOutOfStock = x.IsOutOfStock
            })
            .ToList();

        var transactions = await _context.InventoryTransactions
            .Include(x => x.Warehouse).Include(x => x.Employee)
            .Where(x => x.MaterialItemId == materialItemId)
            .OrderByDescending(x => x.TransactionDate)
            .Take(20)
            .Select(x => new InventoryTransactionRowDto
            {
                InventoryTransactionId = x.InventoryTransactionId,
                MaterialCode = item.MaterialCode,
                MaterialDescription = item.Description,
                WarehouseCode = x.Warehouse != null ? x.Warehouse.Code : "",
                TransactionType = x.TransactionType,
                Quantity = x.Quantity,
                ReferenceType = x.ReferenceType,
                ReferenceId = x.ReferenceId,
                EmployeeName = x.Employee != null ? x.Employee.FirstName + " " + x.Employee.LastName : null,
                TransactionDate = x.TransactionDate,
                Reason = x.Reason
            })
            .ToListAsync();

        // طبق سند Metadata Extraction بخش ۹ (Material → Metadata → Import Batch → Source
        // Document): یک متریال ممکن است از چند ردیف Import (وارداتِ مکرر همان متریال) بیاید
        var sourceImports = await _context.MrsImportRows
            .Include(x => x.Batch)
            .Where(x => x.ResultMaterialItemId == materialItemId && x.Batch != null)
            .OrderByDescending(x => x.Batch!.ImportDate)
            .Select(x => new MaterialSourceImportDto
            {
                MrsImportBatchId = x.Batch!.MrsImportBatchId,
                SourceFileName = x.Batch.SourceFileName,
                ImportDate = x.Batch.ImportDate,
                RawMrsNo = x.RawMrsNo,
                RawSupplier = x.RawSupplier,
                RawPoNo = x.RawPoNo
            })
            .ToListAsync();

        return new MaterialItemDetailDto
        {
            MaterialItemId = item.MaterialItemId,
            MaterialCode = item.MaterialCode,
            MaterialType = item.MaterialType,
            Description = item.Description,
            LongDescription = item.LongDescription,
            CategoryId = item.CategoryId,
            CategoryName = item.Category?.Name,
            DisciplineId = item.DisciplineId,
            DisciplineName = item.Discipline?.Name,
            UomId = item.UomId,
            UomCode = item.Uom?.Code,
            Manufacturer = item.Manufacturer,
            ManufacturerPartNumber = item.ManufacturerPartNumber,
            VendorId = item.VendorId,
            VendorName = item.Vendor?.Name,
            Specification = item.Specification,
            Model = item.Model,
            Size = item.Size,
            MaterialGrade = item.MaterialGrade,
            IsSerialized = item.IsSerialized,
            IsBatchControlled = item.IsBatchControlled,
            IsStockItem = item.IsStockItem,
            IsCritical = item.IsCritical,
            IsActive = item.IsActive,
            Stocks = stocks,
            RecentTransactions = transactions,
            SourceImports = sourceImports
        };
    }

    public async Task<(bool Success, string? Error, int? MaterialItemId)> CreateAsync(
        string materialCode, string description, string? materialType, string? longDescription,
        int? categoryId, int? disciplineId, int? uomId, string? manufacturer, string? manufacturerPartNumber,
        int? vendorId, string? specification, string? model, string? size, string? materialGrade,
        bool isSerialized, bool isBatchControlled, bool isStockItem, bool isCritical, string? currentUserName)
    {
        materialCode = materialCode?.Trim() ?? "";
        description = description?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(materialCode)) return (false, "کد متریال الزامی است.", null);
        if (string.IsNullOrWhiteSpace(description)) return (false, "شرح متریال الزامی است.", null);

        if (await _context.MaterialItems.AnyAsync(x => x.MaterialCode == materialCode))
            return (false, $"کد متریال «{materialCode}» قبلاً استفاده شده است.", null);

        var item = new MaterialItem
        {
            MaterialCode = materialCode,
            Description = description,
            MaterialType = materialType,
            LongDescription = longDescription,
            CategoryId = categoryId,
            DisciplineId = disciplineId,
            UomId = uomId,
            Manufacturer = manufacturer,
            ManufacturerPartNumber = manufacturerPartNumber,
            VendorId = vendorId,
            Specification = specification,
            Model = model,
            Size = size,
            MaterialGrade = materialGrade,
            IsSerialized = isSerialized,
            IsBatchControlled = isBatchControlled,
            IsStockItem = isStockItem,
            IsCritical = isCritical,
            IsActive = true,
            CreatedBy = currentUserName,
            CreatedDate = DateTime.Now
        };

        await _context.MaterialItems.AddAsync(item);
        await _context.SaveChangesAsync();

        return (true, null, item.MaterialItemId);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(
        int materialItemId, string materialCode, string description, string? materialType, string? longDescription,
        int? categoryId, int? disciplineId, int? uomId, string? manufacturer, string? manufacturerPartNumber,
        int? vendorId, string? specification, string? model, string? size, string? materialGrade,
        bool isSerialized, bool isBatchControlled, bool isStockItem, bool isCritical, string? currentUserName)
    {
        var item = await _context.MaterialItems.FirstOrDefaultAsync(x => x.MaterialItemId == materialItemId);
        if (item == null) return (false, "متریال پیدا نشد.");

        materialCode = materialCode?.Trim() ?? "";
        description = description?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(materialCode)) return (false, "کد متریال الزامی است.");
        if (string.IsNullOrWhiteSpace(description)) return (false, "شرح متریال الزامی است.");

        if (await _context.MaterialItems.AnyAsync(x => x.MaterialItemId != materialItemId && x.MaterialCode == materialCode))
            return (false, $"کد متریال «{materialCode}» قبلاً استفاده شده است.");

        item.MaterialCode = materialCode;
        item.Description = description;
        item.MaterialType = materialType;
        item.LongDescription = longDescription;
        item.CategoryId = categoryId;
        item.DisciplineId = disciplineId;
        item.UomId = uomId;
        item.Manufacturer = manufacturer;
        item.ManufacturerPartNumber = manufacturerPartNumber;
        item.VendorId = vendorId;
        item.Specification = specification;
        item.Model = model;
        item.Size = size;
        item.MaterialGrade = materialGrade;
        item.IsSerialized = isSerialized;
        item.IsBatchControlled = isBatchControlled;
        item.IsStockItem = isStockItem;
        item.IsCritical = isCritical;
        item.ModifiedBy = currentUserName;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task ToggleActiveAsync(int materialItemId, string? currentUserName)
    {
        var item = await _context.MaterialItems.FirstOrDefaultAsync(x => x.MaterialItemId == materialItemId);
        if (item == null) return;

        item.IsActive = !item.IsActive;
        item.ModifiedBy = currentUserName;
        await _context.SaveChangesAsync();
    }
}
