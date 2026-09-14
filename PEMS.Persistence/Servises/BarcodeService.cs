using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS — Enterprise Barcode System»: هسته‌ی مشترک شناسایی/چرخه‌عمر/Resolve - یک سرویس
/// واحد برای همه‌ی TargetTypeها (Equipment/MaterialItem/Warehouse/StorageLocation و در آینده
/// هرچه ممیزی تایید کند)، نه یک سرویس جدا به‌ازای هر ماژول.
/// </summary>
public class BarcodeService : IBarcodeService
{
    // طبق بند «Barcode Identity Model»: هرگز مشتق از TargetId خام نیست - یک توکن تصادفی
    // رمزنگاری‌شده. الفبای Crockford-مانند (بدون 0/O/1/I) برای خوانایی روی برچسب چاپی.
    private const string TokenAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly PEMSDbContext _context;

    public BarcodeService(PEMSDbContext context)
    {
        _context = context;
    }

    public static readonly HashSet<string> SupportedTargetTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Equipment", "MaterialItem", "Warehouse", "StorageLocation"
    };

    // ساده و قطعی: هر بایت تصادفی مستقیم به یک نویسه از الفبا نگاشت می‌شود - بدون هیچ فرمول پیچیده
    private static string GenerateToken()
    {
        var raw = RandomNumberGenerator.GetBytes(16);
        var token = new char[16];
        for (var i = 0; i < 16; i++)
            token[i] = TokenAlphabet[raw[i] % TokenAlphabet.Length];

        return "PEMS-BR-" + new string(token);
    }

    public async Task<(BarcodeIdentityDto Barcode, bool WasExisting)> GenerateAsync(string targetType, int targetId, string symbology, int? createdById)
    {
        if (!SupportedTargetTypes.Contains(targetType))
            throw new InvalidOperationException($"نوع هدف «{targetType}» پشتیبانی نمی‌شود.");

        // مسیر سریع: اگر بارکد Active از قبل وجود دارد، همان را برمی‌گرداند - طبق بند
        // «Idempotent generation». Race واقعی توسط ایندکس یکتای فیلترشده‌ی پایین‌تر مدیریت می‌شود.
        var existing = await _context.BarcodeIdentities
            .FirstOrDefaultAsync(x => x.TargetType == targetType && x.TargetId == targetId && x.Status == "Active");
        if (existing != null)
            return (await MapAsync(existing), true);

        // طبق بند ممیزی نهایی «Bulk Generation Safety»/«Database Integrity»: هرگز یک BarcodeIdentity
        // یتیم (اشاره به رکورد واقعا موجودنبودن) ساخته نشود - قبل از درج، خودِ رکورد هدف بررسی می‌شود.
        if (!await TargetExistsAsync(targetType, targetId))
            throw new InvalidOperationException($"رکورد هدف ({targetType} #{targetId}) پیدا نشد.");

        var entity = new BarcodeIdentity
        {
            Code = GenerateToken(),
            Symbology = symbology,
            TargetType = targetType,
            TargetId = targetId,
            Status = "Active",
            CreatedById = createdById,
            CreatedDate = DateTime.Now
        };

        await _context.BarcodeIdentities.AddAsync(entity);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // طبق بند «Concurrency-safe creation»: دو درخواست هم‌زمان برای همون Target - ایندکس
            // یکتای فیلترشده (TargetType+TargetId WHERE Status='Active') برنده را مشخص می‌کند؛
            // بازنده به‌جای خطا، همون رکورد برنده را برمی‌گرداند.
            _context.Entry(entity).State = EntityState.Detached;
            var winner = await _context.BarcodeIdentities
                .FirstOrDefaultAsync(x => x.TargetType == targetType && x.TargetId == targetId && x.Status == "Active");
            if (winner != null)
                return (await MapAsync(winner), true);
            throw;
        }

        return (await MapAsync(entity), false);
    }

    public async Task<BulkGenerateResultDto> BulkGenerateAsync(string targetType, List<int> targetIds, string symbology, int? createdById)
    {
        var result = new BulkGenerateResultDto { Requested = targetIds.Count };

        if (!SupportedTargetTypes.Contains(targetType))
        {
            result.Failed = targetIds.Count;
            result.Errors.Add($"نوع هدف «{targetType}» پشتیبانی نمی‌شود.");
            return result;
        }

        // طبق بند «Bulk Barcode Generation»/«Performance»: یک Query برای همه‌ی بارکدهای Active
        // موجود (نه یک Query جدا به‌ازای هر TargetId).
        var distinctIds = targetIds.Distinct().ToList();
        var existingByTarget = await _context.BarcodeIdentities
            .Where(x => x.TargetType == targetType && distinctIds.Contains(x.TargetId) && x.Status == "Active")
            .ToDictionaryAsync(x => x.TargetId, x => x);

        // طبق بند ممیزی نهایی «Bulk Generation Safety»: شناسه‌های ارسالی سمت سرور دوباره در
        // برابر رکورد هدف واقعی اعتبارسنجی می‌شوند (نه فقط اعتماد به فیلتر GET قبلی که ممکن است
        // بین رندر صفحه و ارسال فرم، رکوردی حذف/غیرفعال شده باشد) - یک Query واحد برای کل دسته.
        var existingTargetIds = await GetExistingTargetIdsAsync(targetType, distinctIds);

        var toCreate = new List<BarcodeIdentity>();
        foreach (var id in distinctIds)
        {
            if (existingByTarget.ContainsKey(id))
            {
                result.AlreadyExisted++;
                continue;
            }

            if (!existingTargetIds.Contains(id))
            {
                result.Failed++;
                result.Errors.Add($"رکورد هدف ({targetType} #{id}) پیدا نشد - رد شد.");
                continue;
            }

            toCreate.Add(new BarcodeIdentity
            {
                Code = GenerateToken(),
                Symbology = symbology,
                TargetType = targetType,
                TargetId = id,
                Status = "Active",
                CreatedById = createdById,
                CreatedDate = DateTime.Now
            });
        }

        if (toCreate.Count > 0)
        {
            await _context.BarcodeIdentities.AddRangeAsync(toCreate);

            try
            {
                await _context.SaveChangesAsync(); // مسیر سریع: یک SaveChangesAsync برای کل دسته - نه به‌ازای هر ردیف
                result.Created = toCreate.Count;
                result.BarcodeIdentityIds = toCreate.Select(x => x.BarcodeIdentityId).ToList();
            }
            catch (DbUpdateException)
            {
                // طبق بند ممیزی نهایی «handles duplicate/concurrent requests safely»: یک Bulk دیگر
                // هم‌زمان همون Target را پوشش داده - ایندکس یکتای فیلترشده کل دسته را رد کرد.
                // به‌جای شکست کل درخواست (و از دست رفتن ردیف‌های بدون تداخل)، هر ردیف با همون
                // مسیر Idempotent تک‌هدفِ GenerateAsync (که Race را خودش امن مدیریت می‌کند)
                // دوباره پردازش می‌شود - فقط در این مسیر نادر، نه در مسیر عادی/سریع بالا.
                foreach (var entry in toCreate)
                    _context.Entry(entry).State = EntityState.Detached;

                foreach (var entry in toCreate)
                {
                    var (barcode, wasExisting) = await GenerateAsync(targetType, entry.TargetId, entry.Symbology, entry.CreatedById);
                    if (wasExisting) result.AlreadyExisted++;
                    else result.Created++;
                    result.BarcodeIdentityIds.Add(barcode.BarcodeIdentityId);
                }
            }
        }

        return result;
    }

    public async Task<BarcodeIdentityDto?> GetByIdAsync(int barcodeIdentityId)
    {
        var entity = await _context.BarcodeIdentities.Include(x => x.CreatedBy)
            .FirstOrDefaultAsync(x => x.BarcodeIdentityId == barcodeIdentityId);
        return entity == null ? null : await MapAsync(entity);
    }

    public async Task<BarcodeIdentityDto?> GetActiveByTargetAsync(string targetType, int targetId)
    {
        var entity = await _context.BarcodeIdentities.Include(x => x.CreatedBy)
            .FirstOrDefaultAsync(x => x.TargetType == targetType && x.TargetId == targetId && x.Status == "Active");
        return entity == null ? null : await MapAsync(entity);
    }

    public async Task<List<BarcodeIdentityDto>> GetHistoryByTargetAsync(string targetType, int targetId)
    {
        var entities = await _context.BarcodeIdentities.Include(x => x.CreatedBy)
            .Where(x => x.TargetType == targetType && x.TargetId == targetId)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        var result = new List<BarcodeIdentityDto>();
        foreach (var e in entities) result.Add(await MapAsync(e));
        return result;
    }

    // طبق بند «Barcode Resolution»/«Performance»: فقط ستون‌های لازم برای تصمیم مسیریابی -
    // بدون Include هیچ رابطه‌ای، بدون بارگذاری رکورد هدف واقعی.
    public async Task<BarcodeResolutionResultDto> ResolveAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new BarcodeResolutionResultDto { Success = false, ErrorCode = "BARCODE_INVALID", ErrorMessage = "بارکد خالی است." };

        var row = await _context.BarcodeIdentities
            .Where(x => x.Code == code.Trim())
            .Select(x => new { x.BarcodeIdentityId, x.TargetType, x.TargetId, x.Status })
            .FirstOrDefaultAsync();

        if (row == null)
            return new BarcodeResolutionResultDto { Success = false, ErrorCode = "BARCODE_NOT_FOUND", ErrorMessage = "این بارکد در PEMS شناخته‌شده نیست." };

        if (row.Status == "Disabled")
            return new BarcodeResolutionResultDto { Success = false, ErrorCode = "BARCODE_DISABLED", TargetType = row.TargetType, TargetId = row.TargetId, Status = row.Status };

        if (row.Status is "Revoked" or "Replaced")
            return new BarcodeResolutionResultDto { Success = false, ErrorCode = "BARCODE_REVOKED", TargetType = row.TargetType, TargetId = row.TargetId, Status = row.Status };

        return new BarcodeResolutionResultDto
        {
            Success = true,
            BarcodeIdentityId = row.BarcodeIdentityId,
            TargetType = row.TargetType,
            TargetId = row.TargetId,
            Status = row.Status
        };
    }

    public async Task<(bool Success, string? Error)> RecordPrintAsync(int barcodeIdentityId)
    {
        var entity = await _context.BarcodeIdentities.FirstOrDefaultAsync(x => x.BarcodeIdentityId == barcodeIdentityId);
        if (entity == null) return (false, "بارکد پیدا نشد.");
        if (entity.Status != "Active") return (false, "فقط بارکد Active قابل چاپ است.");

        entity.PrintCount++;
        entity.LastPrintedDate = DateTime.Now;
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DisableAsync(int barcodeIdentityId, string? reason, int? disabledById)
    {
        var entity = await _context.BarcodeIdentities.FirstOrDefaultAsync(x => x.BarcodeIdentityId == barcodeIdentityId);
        if (entity == null) return (false, "بارکد پیدا نشد.");
        if (entity.Status != "Active") return (false, "فقط بارکد Active قابل غیرفعال‌سازی است.");

        entity.Status = "Disabled";
        entity.DisabledDate = DateTime.Now;
        entity.DisabledById = disabledById;
        entity.DisableReason = reason;
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RevokeAsync(int barcodeIdentityId, string? reason, int? revokedById)
    {
        var entity = await _context.BarcodeIdentities.FirstOrDefaultAsync(x => x.BarcodeIdentityId == barcodeIdentityId);
        if (entity == null) return (false, "بارکد پیدا نشد.");
        if (entity.Status is "Revoked" or "Replaced") return (false, "این بارکد از قبل Revoked/Replaced است.");

        // طبق بند «Do not silently delete barcode history»: فقط Status تغییر می‌کند - رکورد حذف نمی‌شود
        entity.Status = "Revoked";
        entity.RevokedDate = DateTime.Now;
        entity.RevokedById = revokedById;
        entity.RevokeReason = reason;
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(BarcodeIdentityDto? NewBarcode, string? Error)> ReplaceAsync(int oldBarcodeIdentityId, string symbology, int? createdById)
    {
        var old = await _context.BarcodeIdentities.FirstOrDefaultAsync(x => x.BarcodeIdentityId == oldBarcodeIdentityId);
        if (old == null) return (null, "بارکد قدیمی پیدا نشد.");
        if (old.Status is "Revoked" or "Replaced") return (null, "این بارکد از قبل Revoked/Replaced است.");

        old.Status = "Replaced";
        old.RevokedDate = DateTime.Now;
        old.RevokedById = createdById;
        old.RevokeReason = "Replaced by a new barcode";

        var newEntity = new BarcodeIdentity
        {
            Code = GenerateToken(),
            Symbology = symbology,
            TargetType = old.TargetType,
            TargetId = old.TargetId,
            Status = "Active",
            CreatedById = createdById,
            CreatedDate = DateTime.Now,
            ReplacesBarcodeIdentityId = old.BarcodeIdentityId
        };

        await _context.BarcodeIdentities.AddAsync(newEntity);
        await _context.SaveChangesAsync();

        old.ReplacedByBarcodeIdentityId = newEntity.BarcodeIdentityId;
        await _context.SaveChangesAsync();

        return (await MapAsync(newEntity), null);
    }

    // طبق بند ممیزی نهایی «Database Integrity»: بررسی وجود واقعی رکورد هدف پیش از درج
    // BarcodeIdentity - جلوگیری از رکورد یتیم (اشاره به Equipment/MaterialItem/... که اصلا وجود ندارد).
    private async Task<bool> TargetExistsAsync(string targetType, int targetId) => targetType switch
    {
        "Equipment" => await _context.Equipments.AnyAsync(x => x.EquipmentId == targetId),
        "MaterialItem" => await _context.MaterialItems.AnyAsync(x => x.MaterialItemId == targetId),
        "Warehouse" => await _context.Warehouses.AnyAsync(x => x.WarehouseId == targetId),
        "StorageLocation" => await _context.StorageLocations.AnyAsync(x => x.StorageLocationId == targetId),
        _ => false
    };

    // همون بررسی بالا ولی یک‌جا برای کل دسته (Bulk) - یک Query به‌ازای هر نوع، نه N+1.
    private async Task<HashSet<int>> GetExistingTargetIdsAsync(string targetType, List<int> targetIds) => targetType switch
    {
        "Equipment" => (await _context.Equipments.Where(x => targetIds.Contains(x.EquipmentId)).Select(x => x.EquipmentId).ToListAsync()).ToHashSet(),
        "MaterialItem" => (await _context.MaterialItems.Where(x => targetIds.Contains(x.MaterialItemId)).Select(x => x.MaterialItemId).ToListAsync()).ToHashSet(),
        "Warehouse" => (await _context.Warehouses.Where(x => targetIds.Contains(x.WarehouseId)).Select(x => x.WarehouseId).ToListAsync()).ToHashSet(),
        "StorageLocation" => (await _context.StorageLocations.Where(x => targetIds.Contains(x.StorageLocationId)).Select(x => x.StorageLocationId).ToListAsync()).ToHashSet(),
        _ => new HashSet<int>()
    };

    // طبق بند «Verification»: برچسب خوانا برای انسان فقط در لحظه‌ی نمایش، از رکورد هدف واقعی
    // خوانده می‌شود - هرگز در خودِ بارکد ذخیره/کدگذاری نشده است (طبق بند «Barcode Identity Model»).
    private async Task<string?> ResolveTargetDisplayLabelAsync(string targetType, int targetId)
    {
        return targetType switch
        {
            "Equipment" => await _context.Equipments.Where(x => x.EquipmentId == targetId)
                .Select(x => x.TagNumber + " - " + x.Name).FirstOrDefaultAsync(),
            "MaterialItem" => await _context.MaterialItems.Where(x => x.MaterialItemId == targetId)
                .Select(x => x.MaterialCode + " - " + x.Description).FirstOrDefaultAsync(),
            "Warehouse" => await _context.Warehouses.Where(x => x.WarehouseId == targetId)
                .Select(x => x.Code + " - " + x.Name).FirstOrDefaultAsync(),
            "StorageLocation" => await _context.StorageLocations.Where(x => x.StorageLocationId == targetId)
                .Select(x => x.Code + " - " + x.Name).FirstOrDefaultAsync(),
            _ => null
        };
    }

    private async Task<BarcodeIdentityDto> MapAsync(BarcodeIdentity e) => new()
    {
        BarcodeIdentityId = e.BarcodeIdentityId,
        Code = e.Code,
        Symbology = e.Symbology,
        TargetType = e.TargetType,
        TargetId = e.TargetId,
        Status = e.Status,
        CreatedByName = e.CreatedBy != null ? $"{e.CreatedBy.FirstName} {e.CreatedBy.LastName}" : null,
        CreatedDate = e.CreatedDate,
        PrintCount = e.PrintCount,
        LastPrintedDate = e.LastPrintedDate,
        DisableReason = e.DisableReason,
        RevokeReason = e.RevokeReason,
        ReplacedByBarcodeIdentityId = e.ReplacedByBarcodeIdentityId,
        ReplacesBarcodeIdentityId = e.ReplacesBarcodeIdentityId,
        TargetDisplayLabel = await ResolveTargetDisplayLabelAsync(e.TargetType, e.TargetId)
    };
}
