using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» فاز ۱: کنترلر نازک برای
/// ProductionRun (شروع/توقف/تکمیل/لغو) + ثبت Input/Output/Loss + Post اختیاری به موجودی
/// (IMaterialStockService موجود، بدون سیستم موجودی دوم) + گذار QualityStatus بچ. همه‌ی منطق
/// در IProductionRunService.
/// </summary>
public class ProductionRunController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly IProductionRunService _runService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly IAccessControlService _accessControlService;

    public ProductionRunController(
        PEMSDbContext context, IProductionRunService runService,
        ICurrentUserService currentUser, IPermissionService permissionService, IAccessControlService accessControlService)
    {
        _context = context;
        _runService = runService;
        _currentUser = currentUser;
        _permissionService = permissionService;
        _accessControlService = accessControlService;
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "View"))
            return Forbid();

        var run = await _runService.GetByIdAsync(id);
        if (run == null) return NotFound();

        ViewData["Title"] = $"Production Run {run.RunNumber}";
        ViewData["Section"] = "Operations & Production";

        ViewBag.Warehouses = await _context.Warehouses.Where(x => x.IsActive)
            .OrderBy(x => x.Code).Select(x => new { x.WarehouseId, x.Code, x.Name }).ToListAsync();

        ViewBag.MaterialItems = await _context.MaterialItems.Where(x => x.IsActive)
            .OrderBy(x => x.MaterialCode)
            .Select(x => new { x.MaterialItemId, x.MaterialCode, x.Description, x.UomId }).ToListAsync();

        ViewBag.LossCategories = ProductionRunService.LossCategories;

        return View(run);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int productionOrderId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Create"))
            return Forbid();

        var order = await _context.ProductionOrders.Include(x => x.MaterialItem)
            .FirstOrDefaultAsync(x => x.ProductionOrderId == productionOrderId);
        if (order == null) return NotFound();

        ViewData["Title"] = "New Production Run";
        ViewData["Section"] = "Operations & Production";
        ViewBag.Order = order;

        ViewBag.Employees = await _context.Employees.Where(x => x.IsActive)
            .OrderBy(x => x.FirstName).Select(x => new { x.EmployeeId, Name = x.FirstName + " " + x.LastName }).ToListAsync();
        ViewBag.Shifts = await _context.ShiftDefinitions.Where(x => x.IsActive)
            .OrderBy(x => x.Code).Select(x => new { x.ShiftDefinitionId, x.Code, x.Name }).ToListAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int productionOrderId, int? equipmentId, int? operatorEmployeeId, int? shiftDefinitionId, string? notes)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Create"))
            return Forbid();

        var (success, error, runId) = await _runService.CreateAsync(
            productionOrderId, null, equipmentId, operatorEmployeeId, shiftDefinitionId, notes, await GetCurrentEmployeeIdAsync());

        if (!success)
        {
            TempData["Error"] = error;
            return RedirectToAction("Details", "ProductionOrder", new { id = productionOrderId });
        }

        TempData["Message"] = "اجرای تولید ایجاد شد.";
        return RedirectToAction(nameof(Details), new { id = runId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Create"))
            return Forbid();

        var (success, error) = await _runService.StartAsync(id, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "اجرا شروع شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pause(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Create"))
            return Forbid();

        var (success, error) = await _runService.PauseAsync(id, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "اجرا موقتاً متوقف شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resume(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "ReleaseHold"))
            return Forbid();

        var (success, error) = await _runService.ResumeAsync(id, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "اجرا ازسرگیری شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hold(int id, string reason)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Hold"))
            return Forbid();

        var (success, error) = await _runService.HoldAsync(id, reason, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "اجرا معلق شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Create"))
            return Forbid();

        var (success, error) = await _runService.CompleteAsync(id, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "اجرا تکمیل شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string reason)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Delete"))
            return Forbid();

        var (success, error) = await _runService.CancelAsync(id, reason, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "اجرا لغو شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordInput(int productionRunId, int materialItemId, decimal quantity, int? uomId, int? sourceProductionBatchId, string? notes)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Create"))
            return Forbid();

        var (success, error) = await _runService.RecordInputAsync(
            productionRunId, materialItemId, quantity, uomId, sourceProductionBatchId, notes, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "ورودی ثبت شد." : error;
        return RedirectToAction(nameof(Details), new { id = productionRunId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordOutput(int productionRunId, int materialItemId, int? productionBatchId, decimal quantity, int? uomId, string outputClassification, string? notes)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Create"))
            return Forbid();

        var (success, error, _) = await _runService.RecordOutputAsync(
            productionRunId, materialItemId, productionBatchId, quantity, uomId, outputClassification, notes, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "خروجی ثبت شد." : error;
        return RedirectToAction(nameof(Details), new { id = productionRunId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordLoss(int productionRunId, decimal quantity, int? uomId, string lossCategory, string? notes)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Create"))
            return Forbid();

        var (success, error) = await _runService.RecordLossAsync(productionRunId, quantity, uomId, lossCategory, notes, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "افت ثبت شد." : error;
        return RedirectToAction(nameof(Details), new { id = productionRunId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostInputToInventory(int productionInputId, int productionRunId, int warehouseId, int? storageLocationId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Create"))
            return Forbid();

        var (success, error) = await _runService.PostInputToInventoryAsync(productionInputId, warehouseId, storageLocationId, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "مصرف به موجودی ارسال شد." : error;
        return RedirectToAction(nameof(Details), new { id = productionRunId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostOutputToInventory(int productionOutputId, int productionRunId, int warehouseId, int? storageLocationId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Create"))
            return Forbid();

        var (success, error) = await _runService.PostOutputToInventoryAsync(productionOutputId, warehouseId, storageLocationId, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "تولید به موجودی ارسال شد." : error;
        return RedirectToAction(nameof(Details), new { id = productionRunId });
    }

    public async Task<IActionResult> BatchDetails(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "View"))
            return Forbid();

        var batch = await _runService.GetBatchByIdAsync(id);
        if (batch == null) return NotFound();

        ViewData["Title"] = $"Production Batch {batch.BatchNumber}";
        ViewData["Section"] = "Operations & Production";
        return View(batch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitBatchForQc(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Create"))
            return Forbid();

        var (success, error) = await _runService.SubmitBatchForQcAsync(id, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "بچ برای بازرسی کیفیت ارسال شد." : error;
        return RedirectToAction(nameof(BatchDetails), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReleaseBatch(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Approve"))
            return Forbid();

        var sodError = await CheckSoDAsync(id, "Approve", "این بچ را که خودتان ایجاد/ویرایش کرده‌اید نمی‌توانید از نظر کیفیت Release کنید.");
        if (sodError != null) { TempData["Error"] = sodError; return RedirectToAction(nameof(BatchDetails), new { id }); }

        var (success, error) = await _runService.ReleaseBatchAsync(id, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "بچ Release شد." : error;
        return RedirectToAction(nameof(BatchDetails), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BlockBatch(int id, string reason)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Hold"))
            return Forbid();

        var (success, error) = await _runService.BlockBatchAsync(id, reason, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "بچ متوقف شد." : error;
        return RedirectToAction(nameof(BatchDetails), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnblockBatch(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "ReleaseHold"))
            return Forbid();

        var (success, error) = await _runService.UnblockBatchAsync(id, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "توقف بچ برداشته شد." : error;
        return RedirectToAction(nameof(BatchDetails), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectBatch(int id, string reason)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Approve"))
            return Forbid();

        var sodError = await CheckSoDAsync(id, "Approve", "این بچ را که خودتان ایجاد/ویرایش کرده‌اید نمی‌توانید از نظر کیفیت رد کنید.");
        if (sodError != null) { TempData["Error"] = sodError; return RedirectToAction(nameof(BatchDetails), new { id }); }

        var (success, error) = await _runService.RejectBatchAsync(id, reason, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "بچ رد شد." : error;
        return RedirectToAction(nameof(BatchDetails), new { id });
    }

    private async Task<string?> CheckSoDAsync(int productionBatchId, string action, string defaultMessage)
    {
        if (!_currentUser.UserId.HasValue) return null;

        var decision = await _accessControlService.AuthorizeAsync(new AccessControlRequest
        {
            UserId = _currentUser.UserId.Value,
            ResourceName = "Production",
            ActionName = action,
            ResourceId = productionBatchId.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        if (!decision.Allowed && decision.Reason == AccessDecisionReason.DeniedBySoD)
            return string.IsNullOrWhiteSpace(decision.Detail) ? defaultMessage : decision.Detail;

        return null;
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }
}
