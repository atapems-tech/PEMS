using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق ممیزی Package Procurement Contract Finalization (§C/§D، Phase 7.2): تا پیش از این هیچ UI
/// ای برای زنجیره‌ی Tender→Bid→Evaluation→Negotiation→Winner وجود نداشت - فقط لایه‌ی سرویس (Phase 3)
/// و تست‌ها. این کنترلر همان الگوی مجوز درون‌خطی ProcurementPackageController/ContractController
/// را دنبال می‌کند.
/// </summary>
public class TenderController : Controller
{
    private readonly ITenderService _service;
    private readonly IProcurementPackageService _packageService;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUser;
    private readonly PEMSDbContext _context;

    public TenderController(ITenderService service, IProcurementPackageService packageService,
        IPermissionService permissionService, ICurrentUserService currentUser, PEMSDbContext context)
    {
        _service = service;
        _packageService = packageService;
        _permissionService = permissionService;
        _currentUser = currentUser;
        _context = context;
    }

    public async Task<IActionResult> Index(int? procurementPackageId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Tender", "View"))
            return Forbid();

        ViewData["Title"] = "Tenders";
        ViewData["Section"] = "Engineering Management";

        var query = _context.Tenders.Include(x => x.ProcurementPackage).AsQueryable();
        if (procurementPackageId.HasValue)
            query = query.Where(x => x.ProcurementPackageId == procurementPackageId.Value);

        var tenders = await query.OrderByDescending(x => x.TenderId).ToListAsync();
        ViewBag.ProcurementPackageId = procurementPackageId;
        return View(tenders);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Tender", "View"))
            return Forbid();

        var tender = await _context.Tenders
            .Include(x => x.ProcurementPackage)
            .Include(x => x.InvitedVendors).ThenInclude(x => x.Vendor)
            .Include(x => x.Bids).ThenInclude(x => x.Vendor)
            .Include(x => x.Bids).ThenInclude(x => x.Evaluations)
            .Include(x => x.Bids).ThenInclude(x => x.Negotiations)
            .Include(x => x.AwardedBid)
            .FirstOrDefaultAsync(x => x.TenderId == id);

        if (tender == null) return NotFound();

        ViewData["Title"] = "Tender Details";
        ViewData["Section"] = "Engineering Management";

        ViewBag.Vendors = await _context.Vendors.Where(x => x.IsActive)
            .Select(x => new { x.VendorId, x.Name }).ToListAsync();

        // طبق گزارش سخت‌افزاری نهایی (Gap ۲): شفاف‌سازی‌های Technical/Commercial/Contractual
        // مطرح‌شده در طول همین مناقصه - از همان ProcurementCommunication موجود، نه یک معماری تازه
        ViewBag.Clarifications = await _context.ProcurementCommunications
            .Include(x => x.Vendor)
            .Include(x => x.Responses)
            .Where(x => x.TenderId == id)
            .OrderByDescending(x => x.RaisedDate)
            .ToListAsync();

        return View(tender);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RaiseClarification(int tenderId, int procurementPackageId, string type, string subject, string body, int? vendorId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Tender", "Edit"))
            return Forbid();

        await _packageService.AddCommunicationAsync(procurementPackageId, type, subject, body, vendorId, _currentUser.FullName, null, tenderId);
        TempData["Message"] = "Clarification raised";
        return RedirectToAction(nameof(Details), new { id = tenderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RespondClarification(int tenderId, int procurementPackageId, int communicationId, string responseText)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Tender", "Edit"))
            return Forbid();

        await _packageService.RespondToCommunicationAsync(procurementPackageId, communicationId, responseText, _currentUser.FullName);
        TempData["Message"] = "Response recorded";
        return RedirectToAction(nameof(Details), new { id = tenderId });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int procurementPackageId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Tender", "Create"))
            return Forbid();

        ViewData["Title"] = "New Tender";
        ViewData["Section"] = "Engineering Management";
        ViewBag.ProcurementPackageId = procurementPackageId;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int procurementPackageId, string title, DateTime? issueDate,
        DateTime? responseDeadline, bool requiresVendorQualification, string? technicalRequirements,
        string? commercialRequirements, string? inspectionRequirements, string? contractualRequirements)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Tender", "Create"))
            return Forbid();

        var id = await _service.CreateAsync(procurementPackageId, title, issueDate, responseDeadline,
            requiresVendorQualification, technicalRequirements, commercialRequirements,
            inspectionRequirements, contractualRequirements);

        TempData["Message"] = "Tender created";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InviteVendor(int tenderId, int vendorId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Tender", "Edit"))
            return Forbid();

        var result = await _service.InviteVendorAsync(tenderId, vendorId);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "Vendor invited";
        return RedirectToAction(nameof(Details), new { id = tenderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenForBidding(int tenderId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Tender", "Edit"))
            return Forbid();

        var result = await _service.OpenForBiddingAsync(tenderId);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "Tender opened for bidding";
        return RedirectToAction(nameof(Details), new { id = tenderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseBidding(int tenderId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Tender", "Edit"))
            return Forbid();

        var result = await _service.CloseBiddingAsync(tenderId);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "Bidding closed";
        return RedirectToAction(nameof(Details), new { id = tenderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitBid(int tenderId, int vendorId, decimal originalValue, string currency,
        string? technicalSubmissionReference, string? commercialSubmissionReference)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Tender", "Edit"))
            return Forbid();

        var result = await _service.SubmitBidAsync(tenderId, vendorId, originalValue, currency,
            technicalSubmissionReference, commercialSubmissionReference);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "Bid submitted";
        return RedirectToAction(nameof(Details), new { id = tenderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordEvaluation(int tenderId, int bidId, string evaluationType, string decision, string? comment)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Tender", "Edit"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var result = await _service.RecordBidEvaluationAsync(bidId, evaluationType, employeeId, null, decision, comment);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = $"{evaluationType} evaluation recorded";
        return RedirectToAction(nameof(Details), new { id = tenderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestNegotiation(int tenderId, int bidId, decimal negotiatedValue,
        string? participants, string? justification)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Tender", "Edit"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var result = await _service.RequestNegotiationAsync(bidId, negotiatedValue, participants, null, null, null, justification, null, employeeId);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "Negotiation proposed";
        return RedirectToAction(nameof(Details), new { id = tenderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveNegotiation(int tenderId, int bidNegotiationId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Tender", "Approve"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var result = await _service.ApproveNegotiationAsync(bidNegotiationId, employeeId);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "Negotiation approved";
        return RedirectToAction(nameof(Details), new { id = tenderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectWinner(int tenderId, int bidId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Tender", "Approve"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var result = await _service.SelectWinnerAsync(tenderId, bidId, employeeId);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "Winner selected";
        return RedirectToAction(nameof(Details), new { id = tenderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateContract(int tenderId, string contractNo, DateTime? startDate, DateTime? endDate)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Tender", "Approve"))
            return Forbid();

        var result = await _service.CreateContractFromWinningBidAsync(tenderId, contractNo, startDate, endDate);
        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Details), new { id = tenderId });
        }

        TempData["Message"] = "Contract draft created from winning bid";
        return RedirectToAction("Details", "Contract", new { id = result.ContractId });
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();
    }
}
