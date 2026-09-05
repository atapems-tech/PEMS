using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق درخواست: هر بخش گزارش‌گیری مخصوص خودش را دارد، با فیلترهای گوناگون (پروژه، دیسیپلین،
/// بازه‌ی زمانی، و فیلترهای مخصوص هر بخش مثل وضعیت/اولویت).
/// </summary>
public class ReportController : Controller
{
    private readonly ICorrespondenceReportService _service;
    private readonly IEngineeringReportService _engineeringReportService;
    private readonly IVDRReportService _vdrReportService;
    private readonly ICmmsReportService _cmmsReportService;
    private readonly IProcurementReportService _procurementReportService;
    private readonly PEMSDbContext _context;

    public ReportController(ICorrespondenceReportService service, IEngineeringReportService engineeringReportService,
        IVDRReportService vdrReportService, ICmmsReportService cmmsReportService,
        IProcurementReportService procurementReportService, PEMSDbContext context)
    {
        _service = service;
        _engineeringReportService = engineeringReportService;
        _vdrReportService = vdrReportService;
        _cmmsReportService = cmmsReportService;
        _procurementReportService = procurementReportService;
        _context = context;
    }

    private async Task LoadCommonFiltersAsync(int? projectId, int? disciplineId)
    {
        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();

        ViewBag.SelectedProjectId = projectId;
        ViewBag.SelectedDisciplineId = disciplineId;
    }

    public async Task<IActionResult> Index(int? projectId, int? disciplineId, DateTime? fromDate, DateTime? toDate)
    {
        ViewData["Title"] = "Correspondence Reports";
        ViewData["Section"] = "Correspondence Management";

        var report = await _service.GetReportAsync(projectId, disciplineId, fromDate, toDate);

        await LoadCommonFiltersAsync(projectId, disciplineId);
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        return View(report);
    }

    // طبق درخواست: گزارش‌گیری از بخش‌های مختلف پروژه (Engineering Documents/MDR)
    public async Task<IActionResult> Engineering(int? projectId, int? disciplineId, DateTime? fromDate, DateTime? toDate)
    {
        ViewData["Title"] = "Engineering Documents Report";
        ViewData["Section"] = "Engineering Management";

        var report = await _engineeringReportService.GetReportAsync(projectId, disciplineId, fromDate, toDate);

        await LoadCommonFiltersAsync(projectId, disciplineId);
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        return View(report);
    }

    // طبق درخواست: گزارش‌گیری از بخش VDR
    public async Task<IActionResult> Vdr(int? projectId, int? disciplineId, DateTime? fromDate, DateTime? toDate)
    {
        ViewData["Title"] = "VDR Report";
        ViewData["Section"] = "Engineering Management";

        var report = await _vdrReportService.GetReportAsync(projectId, disciplineId, fromDate, toDate);

        await LoadCommonFiltersAsync(projectId, disciplineId);
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        return View(report);
    }

    // طبق درخواست: گزارش‌گیری از بخش CMMS (Work Order)
    public async Task<IActionResult> Cmms(string? status, string? priority, DateTime? fromDate, DateTime? toDate)
    {
        ViewData["Title"] = "CMMS / Work Order Report";
        ViewData["Section"] = "CMMS Integration";

        var report = await _cmmsReportService.GetReportAsync(status, priority, fromDate, toDate);

        ViewBag.SelectedStatus = status;
        ViewBag.SelectedPriority = priority;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        return View(report);
    }

    // طبق درخواست: گزارش‌گیری از بخش تدارکات (Procurement Package/MR/MRQ)
    public async Task<IActionResult> Procurement(int? projectId, int? disciplineId, DateTime? fromDate, DateTime? toDate)
    {
        ViewData["Title"] = "Procurement Report";
        ViewData["Section"] = "Engineering Management";

        var report = await _procurementReportService.GetReportAsync(projectId, disciplineId, fromDate, toDate);

        await LoadCommonFiltersAsync(projectId, disciplineId);
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        return View(report);
    }
}
