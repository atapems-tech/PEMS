using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// مدیریت تجهیزات + Digital Equipment Passport طبق سند Enterprise Development Guidelines
/// (Equipment Info + Engineering Documents + Vendor Data/VDR + MR/MRQ + Maintenance History).
/// </summary>
public class EquipmentController : Controller
{
    private readonly IEquipmentService _service;
    private readonly PEMSDbContext _context;
    private readonly IFunctionalLocationService _functionalLocationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IReliabilityService _reliabilityService;
    private readonly IInspectionService _inspectionService;
    private readonly IEquipmentClassificationService _classificationService;
    private readonly IMaintenanceIntelligenceService _intelligenceService;
    private readonly IPermissionService _permissionService;
    private readonly IBarcodeService _barcodeService;

    public EquipmentController(IEquipmentService service, PEMSDbContext context,
        IFunctionalLocationService functionalLocationService, ICurrentUserService currentUser,
        IReliabilityService reliabilityService, IInspectionService inspectionService,
        IEquipmentClassificationService classificationService, IMaintenanceIntelligenceService intelligenceService,
        IPermissionService permissionService, IBarcodeService barcodeService)
    {
        _service = service;
        _context = context;
        _functionalLocationService = functionalLocationService;
        _currentUser = currentUser;
        _reliabilityService = reliabilityService;
        _inspectionService = inspectionService;
        _classificationService = classificationService;
        _intelligenceService = intelligenceService;
        _permissionService = permissionService;
        _barcodeService = barcodeService;
    }

    // طبق «PEMS Phase 2B»: بررسی مجوز مشترک برای اکشن‌های تغییردهنده - داده‌های Permission
    // از قبل برای Equipment seed شده بودند (MaintenanceEngineer: Create/Edit) ولی هرگز به
    // کنترلر وصل نشده بودند. صفحات فقط-خواندنی (Index/Details) دست‌نخورده می‌مانند.
    private async Task<bool> EnsurePermissionAsync(string action, string friendlyMessage)
    {
        if (await _permissionService.HasPermissionAsync(_currentUser.Roles, "Equipment", action))
            return true;
        TempData["Error"] = friendlyMessage;
        return false;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۵C - Equipment Reconciliation): قبلاً
    // GetAllAsync کل جدول فیلترشده را با ToListAsync (بدون Skip/Take) بارگذاری می‌کرد. اکنون
    // GetPagedAsync فیلتر/شمارش/صفحه‌بندی را در SQL انجام می‌دهد.
    public async Task<IActionResult> Index(int? projectId, int? disciplineId, string? equipmentType, string? status, string? q, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Equipment";
        ViewData["Section"] = "CMMS Integration";
        ViewData["HelpKey"] = "Equipment.Index";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var equipment = await _service.GetPagedAsync(page, normalizedPageSize, projectId, disciplineId, equipmentType, status, q);
        var dashboard = await _service.GetDashboardAsync(projectId);

        ViewBag.Dashboard = dashboard;

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();

        // طبق درخواست: فیلتر - نوع/وضعیت از روی داده‌ی موجود (نه یک لیست ثابت هاردکد)
        ViewBag.EquipmentTypes = await _context.Equipments.Where(x => x.IsActive && x.EquipmentType != "")
            .Select(x => x.EquipmentType).Distinct().OrderBy(x => x).ToListAsync();

        ViewBag.Statuses = await _context.Equipments.Where(x => x.IsActive)
            .Select(x => x.Status).Distinct().OrderBy(x => x).ToListAsync();

        ViewBag.SelectedProjectId = projectId;
        ViewBag.SelectedDisciplineId = disciplineId;
        ViewBag.SelectedEquipmentType = equipmentType;
        ViewBag.SelectedStatus = status;
        ViewBag.SearchTerm = q;

        return View(equipment);
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "Digital Equipment Passport";
        ViewData["Section"] = "CMMS Integration";
        ViewData["HelpKey"] = "Equipment.Details";

        var passport = await _service.GetPassportAsync(id);
        if (passport == null) return NotFound();

        ViewBag.FunctionalLocations = await _functionalLocationService.GetPickerListAsync();
        ViewBag.Reliability = await _reliabilityService.GetEquipmentReliabilityAsync(id);
        ViewBag.Inspections = await _inspectionService.GetForEquipmentAsync(id);

        // طبق «PEMS Phase 3 — Equipment Classification Completion» بند ۱۶: طبقه‌بندی + هوش
        // نگهداری + پیشنهاد در انتظار بازبینی (در صورت وجود) - در یک بخش جمع‌وجور، نه اضافه‌بار
        // روی صفحه‌ی موجود.
        ViewBag.MaintenanceIntelligence = await _intelligenceService.GetForEquipmentAsync(id);
        ViewBag.PendingClassificationSuggestion = await _classificationService.GetPendingSuggestionForEquipmentAsync(id);
        ViewBag.ClassificationExplanation = await _classificationService.ExplainClassificationAsync(id);

        // طبق «PEMS — Enterprise Barcode System» بند «UI»: تب Barcode مشترک - همون پارتیال
        // _BarcodePanel که MaterialItem/Details هم استفاده می‌کند.
        ViewBag.BarcodePanel = new PEMS.Web.Models.BarcodePanelViewModel
        {
            TargetType = "Equipment",
            TargetId = id,
            ActiveBarcode = await _barcodeService.GetActiveByTargetAsync("Equipment", id),
            CanManage = await _permissionService.HasPermissionAsync(_currentUser.Roles, "Barcode", "Create")
        };

        return View(passport);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignFoundation(int equipmentId, int? functionalLocationId, int? parentEquipmentId, DateTime? installationDate)
    {
        if (!await EnsurePermissionAsync("Edit", "شما مجاز به ویرایش محل استقرار/تجهیز والد این تجهیز نیستید."))
            return RedirectToAction(nameof(Details), new { id = equipmentId });

        var (success, error) = await _service.AssignFoundationAsync(
            equipmentId, functionalLocationId, parentEquipmentId, installationDate, _currentUser.UserName);

        TempData[success ? "Message" : "Error"] = success ? "Functional Location / Parent Equipment updated" : error;
        return RedirectToAction(nameof(Details), new { id = equipmentId });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!await EnsurePermissionAsync("Create", "شما مجاز به ثبت تجهیز جدید نیستید."))
            return RedirectToAction(nameof(Index));

        ViewData["Title"] = "New Equipment";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();

        return View(new Equipment());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Equipment equipment)
    {
        if (!await EnsurePermissionAsync("Create", "شما مجاز به ثبت تجهیز جدید نیستید."))
            return RedirectToAction(nameof(Index));

        var id = await _service.CreateAsync(equipment);
        TempData["Message"] = "Equipment created successfully";
        return RedirectToAction(nameof(Details), new { id });
    }
}
