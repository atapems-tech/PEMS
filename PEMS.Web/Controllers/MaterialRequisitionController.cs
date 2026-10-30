using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// مدیریت MR (Material Requisition) و MRQ (Material Requisition for Quotation) مرتبط با آن.
/// </summary>
public class MaterialRequisitionController : Controller
{
    private readonly IMaterialRequisitionService _service;
    private readonly PEMSDbContext _context;
    private readonly PEMS.Application.Interfaces.ILetterService _letterService;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUser;

    public MaterialRequisitionController(IMaterialRequisitionService service, PEMSDbContext context,
        PEMS.Application.Interfaces.ILetterService letterService, IPermissionService permissionService,
        ICurrentUserService currentUser)
    {
        _service = service;
        _context = context;
        _letterService = letterService;
        _permissionService = permissionService;
        _currentUser = currentUser;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): قبلاً کل جدول با
    // ToListAsync (بدون Skip/Take) بارگذاری می‌شد.
    public async Task<IActionResult> Index(int page = 1, int? pageSize = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaterialRequisition", "View"))
            return Forbid();

        ViewData["Title"] = "MR Management";
        ViewData["HelpKey"] = "MaterialRequisitions.Index";
        ViewData["Section"] = "Engineering Management";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var result = await _service.GetAllAsync(page, normalizedPageSize);
        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaterialRequisition", "View"))
            return Forbid();

        ViewData["Title"] = "MR Details";
        ViewData["Section"] = "Engineering Management";

        var mr = await _service.GetByIdAsync(id);
        if (mr == null) return NotFound();

        ViewBag.RelatedLetters = await _letterService.GetRelatedToMRAsync(id);

        // SCRUM-470: "Create PO" when this MR may feed a purchase order and the user may create one
        if (HttpContext?.RequestServices?.GetService(typeof(IPurchaseOrderApprovalService)) is IPurchaseOrderApprovalService approval
            && await _permissionService.HasPermissionAsync(_currentUser.Roles, "PurchaseOrder", "Create")
            && (await approval.GetRequisitionsForPoAsync()).Any(m => m.Id == id))
        {
            ViewBag.CanCreatePo = true;
            ViewBag.Vendors = await _context.Vendors.AsNoTracking().Where(v => v.IsActive).OrderBy(v => v.Name)
                .Select(v => new { v.VendorId, v.Name }).ToListAsync();
        }

        return View(mr);
    }

    /// <summary>
    /// SCRUM-428: registered parts (completed part catalogue requests and Material Master) for the part-number lookup of the MR item
    /// lines, in the item shape of the PEMS.UI searchable select. Same permission as creating an MR.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> PartLookup(string? q, [FromServices] IPartCatalogueDuplicateService parts)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaterialRequisition", "Create"))
            return Forbid();
        Response.Headers.CacheControl = "no-store";
        var hits = await parts.SearchExistingPartsAsync(q);
        return Json(new
        {
            items = hits.Select(h => new
            {
                value = h.PartNumber,
                text = string.IsNullOrWhiteSpace(h.Description) ? h.PartNumber : $"{h.PartNumber} — {h.Description}",
                sub = string.Join(" · ", new[] { h.ManufacturerName, h.ManufacturerPartNumber }.Where(x => !string.IsNullOrWhiteSpace(x))),
                description = h.Description,
                unit = h.Unit,
            })
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaterialRequisition", "Create"))
            return Forbid();

        ViewData["Title"] = "New MR";
        ViewData["Section"] = "Engineering Management";

        await LoadLookupsAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string mrNumber, int projectId, int disciplineId, string? equipmentTagNumber,
        string description, DateTime? requiredDate, int? responsibleEngineerId,
        List<string> itemDescriptions, List<string?> itemParts, List<double> itemQty, List<string?> itemUnits)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaterialRequisition", "Create"))
            return Forbid();

        var items = new List<(string, string?, double, string?)>();

        for (int i = 0; i < itemDescriptions.Count; i++)
        {
            // SCRUM-428: a row with only a registered part number is kept; the service takes the description from the part
            var part = i < itemParts.Count ? itemParts[i] : null;
            if (string.IsNullOrWhiteSpace(itemDescriptions[i]) && string.IsNullOrWhiteSpace(part)) continue;

            items.Add((
                itemDescriptions[i] ?? string.Empty,
                i < itemParts.Count ? itemParts[i] : null,
                i < itemQty.Count ? itemQty[i] : 1,
                i < itemUnits.Count ? itemUnits[i] : null));
        }

        var (success, error, id) = await _service.CreateAsync(mrNumber, projectId, disciplineId, equipmentTagNumber,
            description, requiredDate, responsibleEngineerId, null, items);

        if (!success)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Create));
        }

        TempData["Message"] = "MR created successfully";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int mrId, string status, [FromServices] IPreProcurementService? preProcurement = null)
    {
        // طبق الگوی WorkOrderController.AdvanceStatus: خودِ ویرایش نیازمند "Edit" است، اما
        // رسیدن به وضعیت تأییدی ("Approved") علاوه‌بر آن نیازمند "Approve" هم هست.
        var requiredAction = status == "Approved" ? "Approve" : "Edit";
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaterialRequisition", requiredAction))
            return Forbid();

        var (success, error) = await _service.ChangeStatusAsync(mrId, status);
        TempData[success ? "Message" : "Error"] = success ? "وضعیت MR به‌روزرسانی شد." : error;

        // SCRUM-471: an MR issued to procurement enters the procurement queue of My Work for the buyer's review
        if (success && status == "Issued to Procurement" && preProcurement != null)
        {
            int? employeeId = null;
            if (_currentUser.UserId.HasValue)
                employeeId = await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
            await preProcurement.RaiseMaterialRequisitionAsync(mrId, new PreProcurementActor(employeeId, _currentUser.Roles, _currentUser.UserName));
        }
        return RedirectToAction(nameof(Details), new { id = mrId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuotation(int mrId, string mrqNumber, string vendorName,
        DateTime? inquiryDate, decimal? quotedAmount, string? currency, string? notes)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaterialRequisition", "Edit"))
            return Forbid();

        var (success, error, _) = await _service.AddQuotationAsync(mrId, mrqNumber, vendorName, inquiryDate, quotedAmount, currency, notes);
        TempData[success ? "Message" : "Error"] = success ? "Quotation (MRQ) added" : error;
        return RedirectToAction(nameof(Details), new { id = mrId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuotationEvaluation(int mrId, int mrqId,
        string technical, string commercial, string approval)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaterialRequisition", "Edit"))
            return Forbid();

        await _service.UpdateQuotationEvaluationAsync(mrqId, technical, commercial, approval);
        TempData["Message"] = "Quotation evaluation updated";
        return RedirectToAction(nameof(Details), new { id = mrId });
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): قبلاً کل جدول با
    // ToListAsync (بدون Skip/Take) بارگذاری می‌شد و Include(MR) غیرضروری بود.
    public async Task<IActionResult> Quotations(int page = 1, int? pageSize = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaterialRequisition", "View"))
            return Forbid();

        ViewData["Title"] = "MRQ Management";
        ViewData["Section"] = "Engineering Management";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var normalizedPage = page < 1 ? 1 : page;

        var query = _context.MaterialRequisitionQuotations.AsQueryable();
        var totalCount = await query.CountAsync();

        var quotations = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.MRQId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(q => new PEMS.Application.DTOs.MRQDto
            {
                MRQId = q.MRQId,
                MRQNumber = q.MRQNumber,
                MRId = q.MRId,
                MRNumber = q.MR!.MRNumber,
                VendorName = q.VendorName,
                InquiryDate = q.InquiryDate,
                QuotedAmount = q.QuotedAmount,
                Currency = q.Currency,
                QuotationStatus = q.QuotationStatus,
                TechnicalEvaluation = q.TechnicalEvaluation,
                CommercialEvaluation = q.CommercialEvaluation,
                ApprovalStatus = q.ApprovalStatus,
                Notes = q.Notes
            })
            .ToListAsync();

        return View(new PagedResult<PEMS.Application.DTOs.MRQDto>
        {
            Items = quotations,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        });
    }

    private async Task LoadLookupsAsync()
    {
        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();

        ViewBag.Engineers = await _context.Employees.Where(x => x.IsActive)
            .Select(x => new { x.EmployeeId, Name = x.FirstName + " " + x.LastName }).ToListAsync();
    }
}
