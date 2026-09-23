using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Seed;
using PEMS.Web.Security;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق ممیزی Material Management (Gap A/B) + «Step 1/2 Closure Reports» + Step 3: سند رسمی
/// دریافت مادی (IRN) به‌همراه گیت QC/Disposition. اصل «Received ≠ Available»: Submit هیچ
/// InventoryTransaction/MaterialStock ای نمی‌سازد؛ Disposition فقط مقدار Accepted/Rejected/Hold
/// را ثبت و گردش‌کار تایید مستقل QA را (از طریق IWorkflowRuntime موجود - عیناً هم‌الگو با
/// QCController.NcrDisposition/NcrApprove) شروع می‌کند؛ فقط بعد از Approve واقعی، فقط مقدار
/// Accepted به موجودی Post می‌شود.
/// </summary>
public class IrnReceiptController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly IIrnReceiptService _lifecycleService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly INumberingService _numberingService;
    private readonly IWorkflowRuntime _runtime;

    private const string IrnModule = IrnDispositionWorkflowSeeder.Module;
    private const string IrnBusinessObjectType = IrnDispositionWorkflowSeeder.BusinessObjectType;

    public IrnReceiptController(PEMSDbContext context, IIrnReceiptService lifecycleService,
        ICurrentUserService currentUser, IPermissionService permissionService, INumberingService numberingService,
        IWorkflowRuntime runtime)
    {
        _context = context;
        _lifecycleService = lifecycleService;
        _currentUser = currentUser;
        _permissionService = permissionService;
        _numberingService = numberingService;
        _runtime = runtime;
    }

    public async Task<IActionResult> Index(string? status, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Incoming Receipts (IRN)";
        ViewData["Section"] = "Inventory & Materials";
        ViewData["HelpKey"] = "IrnReceipt.Index";

        var query = _context.IrnReceipts.Where(x => x.IsActive).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

        var totalCount = await query.CountAsync();

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);

        var receipts = await query
            .OrderByDescending(x => x.ReceivingDate)
            .ThenByDescending(x => x.IrnReceiptId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new IrnReceiptListDto
            {
                IrnReceiptId = x.IrnReceiptId,
                IrnNumber = x.IrnNumber,
                Status = x.Status,
                VendorName = x.Vendor!.Name,
                WarehouseCode = x.Warehouse!.Code,
                ReceivedByName = x.ReceivedBy!.FirstName + " " + x.ReceivedBy!.LastName,
                ReceivingDate = x.ReceivingDate,
                LineCount = x.Lines.Count
            })
            .ToListAsync();

        ViewBag.Vendors = await _context.Vendors.Where(x => x.IsActive)
            .OrderBy(x => x.Name).Select(x => new { x.VendorId, x.Name }).ToListAsync();
        ViewBag.Warehouses = await _context.Warehouses.Where(x => x.IsActive)
            .OrderBy(x => x.Code).Select(x => new { x.WarehouseId, x.Code }).ToListAsync();
        ViewBag.SelectedStatus = status;

        return View(new PagedResult<IrnReceiptListDto>
        {
            Items = receipts,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int vendorId, int warehouseId, DateTime receivingDate, int? purchaseOrderId, string? notes)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "IRN", "Create"))
            return Forbid();

        if (!await _context.Vendors.AnyAsync(x => x.VendorId == vendorId))
        {
            TempData["Error"] = "Selected vendor was not found.";
            return RedirectToAction(nameof(Index));
        }

        if (!await _context.Warehouses.AnyAsync(x => x.WarehouseId == warehouseId))
        {
            TempData["Error"] = "Selected warehouse was not found.";
            return RedirectToAction(nameof(Index));
        }

        if (purchaseOrderId.HasValue && !await _context.PurchaseOrders.AnyAsync(x => x.PoId == purchaseOrderId.Value))
        {
            TempData["Error"] = "Selected purchase order was not found.";
            return RedirectToAction(nameof(Index));
        }

        var employeeId = await GetCurrentEmployeeIdAsync();
        if (!employeeId.HasValue)
        {
            TempData["Error"] = "No employee record is linked to the current user.";
            return RedirectToAction(nameof(Index));
        }

        var receipt = new IrnReceipt
        {
            IrnNumber = await _numberingService.GetNextNumberAsync("IRN"),
            Status = "Draft",
            VendorId = vendorId,
            WarehouseId = warehouseId,
            ReceivingDate = receivingDate == default ? DateTime.Now : receivingDate,
            PurchaseOrderId = purchaseOrderId,
            ReceivedById = employeeId.Value,
            Notes = notes,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.IrnReceipts.AddAsync(receipt);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = receipt.IrnReceiptId });
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "IRN Details";
        ViewData["Section"] = "Inventory & Materials";
        ViewData["HelpKey"] = "IrnReceipt.Details";

        var receipt = await _context.IrnReceipts
            .Include(x => x.Vendor).Include(x => x.Warehouse).Include(x => x.PurchaseOrder).Include(x => x.ReceivedBy)
            .Include(x => x.Lines).ThenInclude(x => x.MaterialItem)
            .Include(x => x.Lines).ThenInclude(x => x.Uom)
            .FirstOrDefaultAsync(x => x.IrnReceiptId == id);

        if (receipt == null) return NotFound();

        ViewBag.MaterialItems = await _context.MaterialItems.Where(x => x.IsActive && x.IsStockItem)
            .OrderBy(x => x.MaterialCode).Select(x => new { x.MaterialItemId, x.MaterialCode, x.Description }).ToListAsync();

        ViewBag.StatusHistory = await _context.IrnReceiptStatusHistories.Include(x => x.ChangedBy)
            .Where(x => x.IrnReceiptId == id).OrderByDescending(x => x.ChangedDate).ToListAsync();

        ViewBag.DispositionRecords = await _context.IrnDispositionRecords.Include(x => x.DispositionedBy)
            .Where(x => x.IrnReceiptId == id).OrderByDescending(x => x.DispositionedDate).ToListAsync();

        return View(receipt);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLine(int irnReceiptId, int materialItemId, decimal receivedQuantity)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "IRN", "Create"))
            return Forbid();

        var receipt = await _context.IrnReceipts.FirstOrDefaultAsync(x => x.IrnReceiptId == irnReceiptId);
        if (receipt == null) return NotFound();

        // طبق دستور صریح Step 2 بخش ۱۳: بدون اصلاح خاموش یک سند دریافت Submit-شده - فقط در
        // Draft می‌توان خط اضافه/تغییر داد.
        if (receipt.Status != "Draft")
        {
            TempData["Error"] = "Lines can only be added while the IRN is in Draft status.";
            return RedirectToAction(nameof(Details), new { id = irnReceiptId });
        }

        if (receivedQuantity <= 0)
        {
            TempData["Error"] = "Received quantity must be greater than zero.";
            return RedirectToAction(nameof(Details), new { id = irnReceiptId });
        }

        if (!await _context.MaterialItems.AnyAsync(x => x.MaterialItemId == materialItemId))
        {
            TempData["Error"] = "Selected material was not found.";
            return RedirectToAction(nameof(Details), new { id = irnReceiptId });
        }

        await _context.IrnReceiptLines.AddAsync(new IrnReceiptLine
        {
            IrnReceiptId = irnReceiptId,
            MaterialItemId = materialItemId,
            ReceivedQuantity = receivedQuantity,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = irnReceiptId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id, string? reason)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "IRN", "Create"))
            return Forbid();

        var (success, error) = await _lifecycleService.SubmitAsync(id, reason, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "IRN submitted for inspection." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "IRN", "Create"))
            return Forbid();

        var (success, error) = await _lifecycleService.CancelAsync(id, reason, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "IRN cancelled." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    // طبق ممیزی Material Management (Gap B) + Step 3: ثبت Disposition (مقدار Accepted/Rejected/
    // Hold هر خط) + شروع گردش‌کار تایید QA - عیناً هم‌الگو با QCController.NcrDisposition
    // (IWorkflowRuntime.StartInstanceAsync مستقیم در Controller، نه در Service، دقیقاً مثل NCR).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disposition(int id, int[] lineId, decimal[] accepted, decimal[] rejected, decimal[] hold, string? notes)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "IRN", "Edit"))
            return Forbid();

        if (lineId.Length != accepted.Length || lineId.Length != rejected.Length || lineId.Length != hold.Length)
        {
            TempData["Error"] = "Disposition input is malformed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var employeeId = await GetCurrentEmployeeIdAsync();
        var lines = Enumerable.Range(0, lineId.Length)
            .Select(i => (LineId: lineId[i], Accepted: accepted[i], Rejected: rejected[i], Hold: hold[i]))
            .ToList();

        var (success, error, _) = await _lifecycleService.RecordDispositionAsync(id, lines, notes, employeeId);
        if (!success)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Details), new { id });
        }

        await _runtime.StartInstanceAsync(IrnModule, IrnBusinessObjectType, id, IrnDispositionWorkflowSeeder.Category, null, employeeId);

        TempData["Message"] = "Disposition recorded - awaiting QA approval.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // طبق ممیزی Material Management (Gap B) + Step 3: تایید/رد Disposition توسط QA - عیناً
    // هم‌الگو با QCController.NcrApprove. Approve => CompleteDispositionAsync(approved:true)
    // مقدار Accepted را اتمیک به موجودی Post می‌کند؛ Reject => خطوط پاک می‌شوند تا بازرس دوباره
    // Disposition را ثبت کند (بازگشت به Submitted، عیناً مثل الگوی NCR Reject→Open).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string decision, string? comment)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "IRN", "Approve"))
            return Forbid();

        var receipt = await _context.IrnReceipts.FirstOrDefaultAsync(x => x.IrnReceiptId == id);
        if (receipt == null) return NotFound();

        if (receipt.Status != "PendingApproval")
        {
            TempData["Error"] = "This IRN is not awaiting approval.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var instance = await _runtime.GetInstanceAsync(IrnBusinessObjectType, id);
        if (instance == null || instance.Status != "Running")
        {
            TempData["Error"] = "The approval workflow for this IRN was not found.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var employeeId = await GetCurrentEmployeeIdAsync();

        try
        {
            await _runtime.ExecuteActionAsync(
                instance.WorkflowInstanceId, decision, employeeId,
                _currentUser.Roles, comment, HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers["User-Agent"].ToString());

            var (success, error) = await _lifecycleService.CompleteDispositionAsync(id, decision == "Approve", employeeId);

            TempData[success ? "Message" : "Error"] = success
                ? (decision == "Approve" ? "Disposition approved - accepted quantity posted to stock." : "Disposition rejected - IRN returned for re-inspection.")
                : error;
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }
}
