using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق ممیزی Package Procurement Contract Finalization (§C/§H، Phase 7.5): PurchaseOrder
/// Entity/Service/DTO از قبل کامل بودند - این کنترلر همان اتصال گمشده است، نه یک معماری تازه.
/// </summary>
public class PurchaseOrderController : Controller
{
    private readonly IPurchaseOrderService _service;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUser;
    private readonly PEMSDbContext _context;
    private readonly IAccessControlService _accessControlService;

    public PurchaseOrderController(IPurchaseOrderService service, IPermissionService permissionService,
        ICurrentUserService currentUser, PEMSDbContext context, IAccessControlService accessControlService)
    {
        _service = service;
        _permissionService = permissionService;
        _currentUser = currentUser;
        _context = context;
        _accessControlService = accessControlService;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "PurchaseOrder", "View"))
            return Forbid();

        ViewData["Title"] = "Purchase Orders";
        ViewData["Section"] = "Contract Management";

        var list = await _service.GetAllAsync();
        return View(list);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "PurchaseOrder", "View"))
            return Forbid();

        ViewData["Title"] = "Purchase Order Details";
        ViewData["Section"] = "Contract Management";

        var po = await _service.GetByIdAsync(id);
        if (po == null) return NotFound();

        // SCRUM-470: the issue button only after approval, and only for the holder of Approve (display; Issue checks again)
        ViewBag.CanIssue = po.Status == PEMS.Domain.Entities.PurchaseOrderStatuses.Approved
            && await _permissionService.HasPermissionAsync(_currentUser.Roles, "PurchaseOrder", "Approve");

        // SCRUM-470: lines and approval by the PO authority table (resolved per request; the constructor stays as the tests build it)
        if (HttpContext?.RequestServices?.GetService(typeof(IPurchaseOrderApprovalService)) is IPurchaseOrderApprovalService approval)
        {
            ViewBag.Approval = await approval.GetAsync(id);
            ViewBag.CanEdit = await _permissionService.HasPermissionAsync(_currentUser.Roles, "PurchaseOrder", "Edit");
            ViewBag.Requisitions = await approval.GetRequisitionsForPoAsync();
            ViewBag.CommodityGroups = await approval.GetCommodityGroupsAsync();
            ViewBag.UserRoles = _currentUser.Roles;
            ViewBag.UserName = _currentUser.UserName;
        }

        // SCRUM-473: delivery warehouse per line, received / open quantities and inspection release
        if (HttpContext?.RequestServices?.GetService(typeof(IPurchaseOrderReceivingService)) is IPurchaseOrderReceivingService receiving)
        {
            ViewBag.Receiving = await receiving.GetPoLinesAsync(id);
            ViewBag.Warehouses = await _context.Warehouses.Where(w => w.IsActive).OrderBy(w => w.Code)
                .Select(w => new { w.WarehouseId, w.Code }).ToListAsync();
            ViewBag.CanReleaseInspection = await _permissionService.HasPermissionAsync(_currentUser.Roles, "IRN", "Approve");
            ViewBag.InspectionPlans = await _context.InspectionTestPlans.Where(p => p.IsActive && p.ProjectId == po.ProjectId)
                .OrderBy(p => p.ITPNumber).Select(p => new { p.InspectionTestPlanId, p.ITPNumber, p.Title }).ToListAsync();
        }
        return View(po);
    }

    // ================================================================== SCRUM-470: lines and approval

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFromRequisition(int mrId, int vendorId, string currency, string? poNumber, [FromServices] IPurchaseOrderApprovalService approval)
    {
        var r = await approval.CreateFromRequisitionAsync(mrId, vendorId, currency, poNumber, await PoActorAsync());
        if (!r.Success)
        {
            TempData["Error"] = Text(r.Error!);
            return RedirectToAction("Details", "MaterialRequisition", new { id = mrId });
        }
        if (_currentUser.UserId.HasValue)   // creator row for the existing SoD policy (Create <-> Approve), as in Create
            await _accessControlService.AuthorizeAsync(new AccessControlRequest
            {
                UserId = _currentUser.UserId.Value, ResourceName = "PurchaseOrder", ActionName = "Create", ResourceId = r.Id!.Value.ToString(),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
        TempData["Message"] = Text("Po_Created");
        return RedirectToAction(nameof(Details), new { id = r.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLinesFromRequisition(int id, int mrId, [FromServices] IPurchaseOrderApprovalService approval)
        => Done(id, await approval.AddLinesFromRequisitionAsync(id, mrId, await PoActorAsync()), "Po_LinesAdded");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateLine(int id, int lineId, decimal quantity, decimal unitPrice, DateTime? requiredDate, bool isStockLine,
        int? commodityGroupId, string? freeTextReason, int? warehouseId, [FromServices] IPurchaseOrderApprovalService approval)
        => Done(id, await approval.UpdateLineAsync(lineId, new PoLineInput(quantity, unitPrice, requiredDate, isStockLine, commodityGroupId, freeTextReason, warehouseId), await PoActorAsync()), "Po_Saved");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLine(int id, int lineId, [FromServices] IPurchaseOrderApprovalService approval)
        => Done(id, await approval.RemoveLineAsync(lineId, await PoActorAsync()), "Po_Saved");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id, [FromServices] IPurchaseOrderApprovalService approval)
        => Done(id, await approval.SubmitAsync(id, await PoActorAsync()), "Po_Submitted");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RouteAgain(int id, [FromServices] IPurchaseOrderApprovalService approval)
        => Done(id, await approval.RouteAsync(id, await PoActorAsync()), "Po_Submitted");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, [FromServices] IPurchaseOrderApprovalService approval)
        => Done(id, await approval.ApproveAsync(id, await PoActorAsync()), "Po_Approved");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reason, [FromServices] IPurchaseOrderApprovalService approval)
        => Done(id, await approval.RejectAsync(id, reason ?? string.Empty, await PoActorAsync()), "Po_Rejected");

    // ================================================================== SCRUM-473: inspection release before shipment

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReleaseInspection(int id, int lineId, int? inspectionTestPlanId, [FromServices] IPurchaseOrderReceivingService receiving)
    {
        var a = await PoActorAsync();
        var r = await receiving.ReleaseInspectionAsync(lineId, inspectionTestPlanId, new PoReceivingActor(a.EmployeeId, a.UserName, a.Roles));
        return Done(id, new PoResult(r.Success, r.Error), "Rcv_InspectionReleased");
    }

    /// <summary>Success may still carry a notice key (for example "responsible not set").</summary>
    private IActionResult Done(int id, PoResult r, string successKey)
    {
        if (!r.Success) TempData["Error"] = Text(r.Error!);
        else TempData["Message"] = Text(r.Error ?? successKey);
        return RedirectToAction(nameof(Details), new { id });
    }

    private string Text(string keyOrText)
    {
        var localizer = HttpContext?.RequestServices?.GetService(typeof(Microsoft.Extensions.Localization.IStringLocalizer<PEMS.Web.Resources.SharedResource>))
            as Microsoft.Extensions.Localization.IStringLocalizer<PEMS.Web.Resources.SharedResource>;
        var l = localizer?[keyOrText];
        return l == null || l.ResourceNotFound ? keyOrText : l.Value;
    }

    private async Task<PoActor> PoActorAsync()
    {
        int? employeeId = null;
        if (_currentUser.UserId.HasValue)
            employeeId = await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
        return new PoActor(employeeId, _currentUser.Roles, _currentUser.UserName);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "PurchaseOrder", "Create"))
            return Forbid();

        ViewData["Title"] = "New Purchase Order";
        ViewData["Section"] = "Contract Management";

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();
        ViewBag.Vendors = await _context.Vendors.Where(x => x.IsActive)
            .Select(x => new { x.VendorId, x.Name }).ToListAsync();
        ViewBag.Contracts = await _context.Contracts
            .Select(x => new { x.ContractId, x.ContractNo }).ToListAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string poNumber, int projectId, int vendorId, int? contractId,
        string? description, decimal? value)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "PurchaseOrder", "Create"))
            return Forbid();

        int id;
        try
        {
            id = await _service.CreateAsync(poNumber, projectId, vendorId, contractId, description, value);
        }
        catch (InvalidOperationException ex)
        {
            // P0: شماره‌ی تکراری/خالی یا مبلغ نامعتبر - ردِ قابل‌فهم به‌جای HTTP 500 یا ثبت رکورد نامعتبر
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Create));
        }

        // ثبت Allow در AuthorizationAuditLog تا سیاست SoD «Create ↔ Approve» بتواند صادرکننده را
        // با سازنده‌ی همان PO مقایسه کند (همان الگوی ProcurementPackage)
        if (_currentUser.UserId.HasValue)
        {
            await _accessControlService.AuthorizeAsync(new AccessControlRequest
            {
                UserId = _currentUser.UserId.Value,
                ResourceName = "PurchaseOrder",
                ActionName = "Create",
                ResourceId = id.ToString(),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
        }

        TempData["Message"] = "Purchase Order created as Draft";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// صدور PO (Draft → Issued). مجوز PurchaseOrder.Approve + SoD (سازنده‌ی PO نمی‌تواند خودش
    /// آن را صادر کند). این مجوز در حال حاضر به هیچ نقشی داده نشده و SystemAdmin هم بای‌پس
    /// ندارد (AccessControlService.RequiresExplicitGrant) - تا زمانی که سیاست سازمانی تعیین و
    /// Grant صریح داده نشود، صدور PO جدید عمداً Fail-Closed است.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(int id)
    {
        var outcome = await ProtectedActionGuard.EvaluateAsync(
            HttpContext, _permissionService, _accessControlService, _currentUser, "PurchaseOrder", "Approve", id.ToString());
        if (outcome == ProtectedActionOutcome.DeniedBySoD)
        {
            TempData["Error"] = "طبق سیاست تفکیک وظایف، شما نمی‌توانید Purchase Order ای را که خودتان ایجاد کرده‌اید صادر کنید.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (outcome != ProtectedActionOutcome.Allowed) return Forbid();

        try
        {
            await _service.IssueAsync(id);
            TempData["Message"] = "Purchase Order issued";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
