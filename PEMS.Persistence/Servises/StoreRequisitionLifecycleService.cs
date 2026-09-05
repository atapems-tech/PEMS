using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق سند Inventory بخش ۱۳-۱۴: Reserved≠Issued - رزرو فقط ReservedQuantity را بالا می‌برد،
/// Issue فقط در مرحله‌ی Issued واقعاً OnHand را کم می‌کند. هر دو یک InventoryTransaction
/// می‌سازند تا موجودی هرگز بدون Transaction تغییر نکند.
/// </summary>
public class StoreRequisitionLifecycleService : IStoreRequisitionLifecycleService
{
    private readonly PEMSDbContext _context;

    public StoreRequisitionLifecycleService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string? Error)> AdvanceStatusAsync(
        int storeRequisitionId, string nextStatus, string? reason, int? changedByEmployeeId)
    {
        var requisition = await _context.StoreRequisitions
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.StoreRequisitionId == storeRequisitionId);

        if (requisition == null) return (false, "Store requisition not found.");

        var fromStatus = requisition.Status;

        if (nextStatus == "Approved")
        {
            requisition.ApprovedById = changedByEmployeeId;
            requisition.ApprovedDate = DateTime.Now;
        }

        if (nextStatus == "Reserved")
        {
            var error = await ReserveLinesAsync(requisition, changedByEmployeeId);
            if (error != null) return (false, error);
        }

        if (nextStatus == "Issued")
        {
            await IssueReservationsAsync(requisition, changedByEmployeeId);
        }

        if (nextStatus is "Rejected" or "Cancelled")
        {
            await ReleaseReservationsAsync(requisition, changedByEmployeeId, reason);
        }

        requisition.Status = nextStatus;

        await _context.StoreRequisitionStatusHistories.AddAsync(new StoreRequisitionStatusHistory
        {
            StoreRequisitionId = requisition.StoreRequisitionId,
            FromStatus = fromStatus,
            ToStatus = nextStatus,
            ChangedById = changedByEmployeeId,
            ChangedDate = DateTime.Now,
            Reason = reason
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    private async Task<string?> ReserveLinesAsync(StoreRequisition requisition, int? changedByEmployeeId)
    {
        foreach (var line in requisition.Lines)
        {
            var quantity = line.ApprovedQuantity ?? line.RequestedQuantity;
            if (quantity <= 0) continue;

            var stock = await _context.MaterialStocks
                .Where(x => x.MaterialItemId == line.MaterialItemId && x.WarehouseId == requisition.WarehouseId && x.IsActive)
                .FirstOrDefaultAsync();

            if (stock == null || stock.AvailableQuantity < quantity)
                return $"موجودی قابل‌رزرو کافی نیست برای متریال شماره {line.MaterialItemId}.";

            stock.ReservedQuantity += quantity;
            stock.LastUpdated = DateTime.Now;

            await _context.MaterialReservations.AddAsync(new MaterialReservation
            {
                MaterialItemId = line.MaterialItemId,
                WarehouseId = requisition.WarehouseId,
                Quantity = quantity,
                Status = "Active",
                StoreRequisitionId = requisition.StoreRequisitionId,
                ReservedById = changedByEmployeeId ?? 0,
                ReservedDate = DateTime.Now,
                IsActive = true,
                CreatedDate = DateTime.Now
            });

            await _context.InventoryTransactions.AddAsync(new InventoryTransaction
            {
                MaterialItemId = line.MaterialItemId,
                WarehouseId = requisition.WarehouseId,
                TransactionType = "Reservation",
                Quantity = quantity,
                ReferenceType = "StoreRequisition",
                ReferenceId = requisition.StoreRequisitionId,
                EmployeeId = changedByEmployeeId,
                TransactionDate = DateTime.Now,
                Reason = $"رزرو برای StoreRequisition #{requisition.RequisitionNumber}"
            });
        }

        return null;
    }

    private async Task IssueReservationsAsync(StoreRequisition requisition, int? changedByEmployeeId)
    {
        var reservations = await _context.MaterialReservations
            .Where(x => x.StoreRequisitionId == requisition.StoreRequisitionId && x.Status == "Active")
            .ToListAsync();

        foreach (var reservation in reservations)
        {
            var stock = await _context.MaterialStocks
                .FirstOrDefaultAsync(x => x.MaterialItemId == reservation.MaterialItemId
                    && x.WarehouseId == reservation.WarehouseId && x.IsActive);

            if (stock == null) continue;

            stock.OnHandQuantity -= reservation.Quantity;
            stock.ReservedQuantity -= reservation.Quantity;
            stock.LastUpdated = DateTime.Now;

            var line = requisition.Lines.FirstOrDefault(x => x.MaterialItemId == reservation.MaterialItemId);
            if (line != null) line.IssuedQuantity += reservation.Quantity;

            reservation.Status = "FullyIssued";

            await _context.InventoryTransactions.AddAsync(new InventoryTransaction
            {
                MaterialItemId = reservation.MaterialItemId,
                WarehouseId = reservation.WarehouseId ?? requisition.WarehouseId,
                TransactionType = "Issue",
                Quantity = reservation.Quantity,
                ReferenceType = "StoreRequisition",
                ReferenceId = requisition.StoreRequisitionId,
                EmployeeId = changedByEmployeeId,
                TransactionDate = DateTime.Now,
                Reason = $"تحویل برای StoreRequisition #{requisition.RequisitionNumber}"
            });
        }
    }

    private async Task ReleaseReservationsAsync(StoreRequisition requisition, int? changedByEmployeeId, string? reason)
    {
        var reservations = await _context.MaterialReservations
            .Where(x => x.StoreRequisitionId == requisition.StoreRequisitionId && x.Status == "Active")
            .ToListAsync();

        foreach (var reservation in reservations)
        {
            var stock = await _context.MaterialStocks
                .FirstOrDefaultAsync(x => x.MaterialItemId == reservation.MaterialItemId
                    && x.WarehouseId == reservation.WarehouseId && x.IsActive);

            if (stock != null)
            {
                stock.ReservedQuantity -= reservation.Quantity;
                stock.LastUpdated = DateTime.Now;
            }

            reservation.Status = "Released";

            await _context.InventoryTransactions.AddAsync(new InventoryTransaction
            {
                MaterialItemId = reservation.MaterialItemId,
                WarehouseId = reservation.WarehouseId ?? requisition.WarehouseId,
                TransactionType = "ReservationRelease",
                Quantity = reservation.Quantity,
                ReferenceType = "StoreRequisition",
                ReferenceId = requisition.StoreRequisitionId,
                EmployeeId = changedByEmployeeId,
                TransactionDate = DateTime.Now,
                Reason = reason ?? $"آزادسازی رزرو StoreRequisition #{requisition.RequisitionNumber}"
            });
        }
    }
}
