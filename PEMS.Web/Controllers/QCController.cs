using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Seed;
using PEMS.Web.Security;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Web.Controllers;

// ==============================================================================
// بخش QC/QA - طبق ممیزی: قبلاً فقط دو Entity اسکلتی (InspectionRecord/InspectionChecklist)
// بدون هیچ Controller/Service/View وجود داشت. این کنترلر پایه‌ی اول را می‌سازد:
// ITP (Inspection & Test Plan) و NCR (Non-Conformance Report) - طبق API Q1/Q2 و ISO 9001
// بند ۸.۷. دیسیپلین‌های QC (QCDiscipline) کاملاً جدا از دیسیپلین‌های مهندسی است.
//
// فاز بعدی (این نسخه): تایید چندنفره‌ی NCR بحرانی روی Workflow Platform، اتصال ITP Line
// Item به بازرسی واقعی، اعلان Hold Point، و گزارش KPI.
// ==============================================================================
public class QCController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkflowRuntime _runtime;

    private const string NcrModule = NcrApprovalWorkflowSeeder.Module;
    private const string NcrBusinessObjectType = "NonConformanceReport";

    public QCController(PEMSDbContext context, ICurrentUserService currentUser, IWorkflowRuntime runtime)
    {
        _context = context;
        _currentUser = currentUser;
        _runtime = runtime;
    }


    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "QC / QA";
        ViewData["Section"] = "QC/QA";
        ViewData["HelpKey"] = "QC.Index";

        ViewBag.OpenNCRCount = await _context.NonConformanceReports.CountAsync(x => x.IsActive && x.Status != "Closed");
        ViewBag.ActiveITPCount = await _context.InspectionTestPlans.CountAsync(x => x.IsActive && x.Status == "Active");

        ViewBag.RecentNCRs = await _context.NonConformanceReports
            .Where(x => x.IsActive)
            .Include(x => x.QCDiscipline)
            .OrderByDescending(x => x.RaisedDate)
            .Take(10)
            .ToListAsync();

        ViewBag.RecentITPs = await _context.InspectionTestPlans
            .Where(x => x.IsActive)
            .Include(x => x.QCDiscipline)
            .OrderByDescending(x => x.CreatedDate)
            .Take(10)
            .ToListAsync();

        return View();
    }


    // =========================
    // ITP
    // =========================

    public async Task<IActionResult> ItpIndex()
    {
        ViewData["Title"] = "Inspection & Test Plans";
        ViewData["Section"] = "QC/QA";

        var itps = await _context.InspectionTestPlans
            .Where(x => x.IsActive)
            .Include(x => x.QCDiscipline)
            .Include(x => x.Equipment)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        return View(itps);
    }

    [HttpGet]
    public async Task<IActionResult> ItpCreate(int? procurementPackageId = null, int? contractId = null)
    {
        ViewData["Title"] = "New ITP";
        ViewData["Section"] = "QC/QA";

        await LoadCommonDropdownsAsync();
        ViewBag.PrefillPackageId = procurementPackageId;
        ViewBag.PrefillContractId = contractId;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ItpCreate(
        string itpNumber, int projectId, int qcDisciplineId, int? equipmentId,
        string title, string? referenceCodeStandard, string? description,
        int? procurementPackageId, int? contractId)
    {
        var itp = new InspectionTestPlan
        {
            ITPNumber = itpNumber,
            ProjectId = projectId,
            QCDisciplineId = qcDisciplineId,
            EquipmentId = equipmentId,
            ProcurementPackageId = procurementPackageId,
            ContractId = contractId,
            Title = title,
            ReferenceCodeStandard = referenceCodeStandard,
            Description = description,
            Status = "Draft",
            Revision = 0,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.InspectionTestPlans.AddAsync(itp);
        await _context.SaveChangesAsync();

        TempData["Message"] = "ITP created";
        return RedirectToAction(nameof(ItpDetails), new { id = itp.InspectionTestPlanId });
    }

    public async Task<IActionResult> ItpDetails(int id)
    {
        ViewData["Title"] = "ITP Details";
        ViewData["Section"] = "QC/QA";

        var itp = await _context.InspectionTestPlans
            .Include(x => x.QCDiscipline)
            .Include(x => x.Equipment)
            .Include(x => x.ProcurementPackage)
            .Include(x => x.Contract)
            .Include(x => x.LineItems).ThenInclude(l => l.QCDiscipline)
            .FirstOrDefaultAsync(x => x.InspectionTestPlanId == id);

        if (itp == null) return NotFound();

        await LoadCommonDropdownsAsync();

        return View(itp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ItpAddLineItem(
        int itpId, string activity, int qcDisciplineId, string inspectionCategory,
        string? acceptanceCriteria, string? referenceCodeStandard, string? responsibleParty)
    {
        var itp = await _context.InspectionTestPlans.FirstOrDefaultAsync(x => x.InspectionTestPlanId == itpId);
        if (itp == null) return NotFound();

        var nextSeq = 1 + (await _context.ITPLineItems
            .Where(x => x.InspectionTestPlanId == itpId)
            .Select(x => (int?)x.SequenceNo)
            .MaxAsync() ?? 0);

        await _context.ITPLineItems.AddAsync(new ITPLineItem
        {
            InspectionTestPlanId = itpId,
            SequenceNo = nextSeq,
            Activity = activity,
            QCDisciplineId = qcDisciplineId,
            InspectionCategory = inspectionCategory,
            AcceptanceCriteria = acceptanceCriteria,
            ReferenceCodeStandard = referenceCodeStandard,
            ResponsibleParty = responsibleParty,
            Status = "Pending",
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = "Line item added";
        return RedirectToAction(nameof(ItpDetails), new { id = itpId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ItpApprove(int id)
    {
        var itp = await _context.InspectionTestPlans.FirstOrDefaultAsync(x => x.InspectionTestPlanId == id);
        if (itp == null) return NotFound();

        if (itp.Status != "Draft")
        {
            TempData["Error"] = "فقط ITP در وضعیت Draft قابل تایید است.";
            return RedirectToAction(nameof(ItpDetails), new { id });
        }

        itp.Status = "Active";
        itp.ApprovedDate = DateTime.Now;
        itp.ApprovedById = await GetCurrentEmployeeIdAsync();

        await _context.SaveChangesAsync();

        TempData["Message"] = "ITP approved and activated";
        return RedirectToAction(nameof(ItpDetails), new { id });
    }


    // ==============================================================================
    // اعلان Hold Point: طبق فاز بعدی - قبل از این، هیچ راهی برای ثبت اینکه «مسئول ذی‌ربط
    // مطلع شد» نبود، در حالی که Hold Point (H) طبق تعریف بدون این اطلاع‌رسانی معنی ندارد.
    // ==============================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ItpNotifyHoldPoint(int lineItemId, int itpId)
    {
        var lineItem = await _context.ITPLineItems.FirstOrDefaultAsync(x => x.ITPLineItemId == lineItemId);
        if (lineItem == null) return NotFound();

        lineItem.NotifiedDate = DateTime.Now;
        lineItem.NotifiedById = await GetCurrentEmployeeIdAsync();
        if (lineItem.Status == "Pending") lineItem.Status = "Notified";

        await _context.SaveChangesAsync();

        TempData["Message"] = "اعلان ثبت شد";
        return RedirectToAction(nameof(ItpDetails), new { id = itpId });
    }


    // ==============================================================================
    // اتصال ITP Line Item به بازرسی واقعی: طبق فاز بعدی - قبل از این، InspectionRecord کاملاً
    // مستقل از ITP ثبت می‌شد (اگر اصلاً ثبت می‌شد، چون هیچ Controller ای نداشت). حالا بازرس
    // مستقیم از روی همان ردیف ITP، نتیجه‌ی واقعی را ثبت می‌کند و وضعیت ردیف خودکار به‌روز می‌شود.
    // ==============================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ItpPerformInspection(
        int lineItemId, int itpId, string result, string? inspector, string? description)
    {
        var lineItem = await _context.ITPLineItems
            .Include(x => x.InspectionTestPlan)
            .FirstOrDefaultAsync(x => x.ITPLineItemId == lineItemId);

        if (lineItem == null) return NotFound();
        if (lineItem.InspectionTestPlan?.EquipmentId == null)
        {
            TempData["Error"] = "این ITP به هیچ تجهیزی وصل نیست - ثبت بازرسی روی ردیف بدون تجهیز ممکن نیست.";
            return RedirectToAction(nameof(ItpDetails), new { id = itpId });
        }

        var record = new InspectionRecord
        {
            EquipmentId = lineItem.InspectionTestPlan.EquipmentId.Value,
            ITPLineItemId = lineItem.ITPLineItemId,
            QCDisciplineId = lineItem.QCDisciplineId,
            InspectionType = lineItem.Activity,
            InspectionDate = DateTime.Now,
            Inspector = inspector,
            Result = result,
            Description = description,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.InspectionRecords.AddAsync(record);

        lineItem.Status = result == "Acceptable" ? "Passed" : "Failed";

        await _context.SaveChangesAsync();

        TempData["Message"] = result == "Acceptable"
            ? "بازرسی ثبت شد - Passed"
            : "بازرسی ثبت شد - Failed. توصیه می‌شه یک NCR برای این ردیف ثبت کنی.";

        return RedirectToAction(nameof(ItpDetails), new { id = itpId });
    }


    // =========================
    // NCR
    // =========================

    public async Task<IActionResult> NcrIndex()
    {
        ViewData["Title"] = "Non-Conformance Reports";
        ViewData["Section"] = "QC/QA";

        var ncrs = await _context.NonConformanceReports
            .Where(x => x.IsActive)
            .Include(x => x.QCDiscipline)
            .Include(x => x.Equipment)
            .OrderByDescending(x => x.RaisedDate)
            .ToListAsync();

        return View(ncrs);
    }

    [HttpGet]
    public async Task<IActionResult> NcrCreate()
    {
        ViewData["Title"] = "New NCR";
        ViewData["Section"] = "QC/QA";

        await LoadCommonDropdownsAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NcrCreate(
        string ncrNumber, int projectId, int qcDisciplineId, int? equipmentId, int? itpLineItemId,
        string description, string severity)
    {
        var ncr = new NonConformanceReport
        {
            NCRNumber = ncrNumber,
            ProjectId = projectId,
            QCDisciplineId = qcDisciplineId,
            EquipmentId = equipmentId,
            ITPLineItemId = itpLineItemId,
            Description = description,
            Severity = severity,
            Status = "Open",
            RaisedById = await GetCurrentEmployeeIdAsync(),
            RaisedDate = DateTime.Now,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.NonConformanceReports.AddAsync(ncr);
        await _context.SaveChangesAsync();

        TempData["Message"] = "NCR raised";
        return RedirectToAction(nameof(NcrDetails), new { id = ncr.NonConformanceReportId });
    }

    public async Task<IActionResult> NcrDetails(int id)
    {
        ViewData["Title"] = "NCR Details";
        ViewData["Section"] = "QC/QA";

        var ncr = await _context.NonConformanceReports
            .Include(x => x.QCDiscipline)
            .Include(x => x.Equipment)
            .Include(x => x.ITPLineItem)
            .FirstOrDefaultAsync(x => x.NonConformanceReportId == id);

        if (ncr == null) return NotFound();

        return View(ncr);
    }

    // ==============================================================================
    // Disposition: طبق ISO 9001 بند ۸.۷ - تعیین تکلیف عدم انطباق قبل از بستن
    // ==============================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NcrDisposition(
        int id, string disposition, string? rootCause, string? correctiveAction, string? preventiveAction)
    {
        var ncr = await _context.NonConformanceReports.FirstOrDefaultAsync(x => x.NonConformanceReportId == id);
        if (ncr == null) return NotFound();

        ncr.Disposition = disposition;
        ncr.RootCause = rootCause;
        ncr.CorrectiveAction = correctiveAction;
        ncr.PreventiveAction = preventiveAction;
        ncr.DispositionedById = await GetCurrentEmployeeIdAsync();
        ncr.DispositionedDate = DateTime.Now;

        // ==============================================================================
        // طبق فاز بعدی: NCR های Critical نمی‌توانند فقط با تصمیم همان کسی که Disposition را
        // ثبت کرده بسته شوند - باید وارد گردش‌کار تایید مستقل مدیر QA شوند. برای
        // Minor/Major مستقیم Dispositioned (رفتار قبلی، بدون سربار اضافی).
        // ==============================================================================
        if (ncr.Severity == "Critical")
        {
            await _runtime.StartInstanceAsync(
                NcrModule, NcrBusinessObjectType, ncr.NonConformanceReportId,
                NcrApprovalWorkflowSeeder.Category, ncr.ProjectId, ncr.DispositionedById);

            ncr.Status = "PendingApproval";
        }
        else
        {
            ncr.Status = "Dispositioned";
        }

        await _context.SaveChangesAsync();

        TempData["Message"] = ncr.Status == "PendingApproval"
            ? "Disposition ثبت شد - در انتظار تایید مدیر QA (چون این NCR بحرانه)"
            : "Disposition recorded";

        return RedirectToAction(nameof(NcrDetails), new { id });
    }


    // ==============================================================================
    // تایید/رد Disposition یک NCR بحرانی توسط مدیر QA. Approve => قابل بستن؛ Reject => برمی‌گردد
    // Open تا Disposition دوباره بررسی/اصلاح شود.
    // ==============================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NcrApprove(int id, string decision, string? comment)
    {
        var ncr = await _context.NonConformanceReports.FirstOrDefaultAsync(x => x.NonConformanceReportId == id);
        if (ncr == null) return NotFound();

        if (ncr.Status != "PendingApproval")
        {
            TempData["Error"] = "این NCR در وضعیت انتظار تایید نیست.";
            return RedirectToAction(nameof(NcrDetails), new { id });
        }

        var instance = await _runtime.GetInstanceAsync(NcrBusinessObjectType, ncr.NonConformanceReportId);
        if (instance == null || instance.Status != "Running")
        {
            TempData["Error"] = "گردش‌کار تایید این NCR یافت نشد.";
            return RedirectToAction(nameof(NcrDetails), new { id });
        }

        try
        {
            await _runtime.ExecuteActionAsync(
                instance.WorkflowInstanceId, decision, await GetCurrentEmployeeIdAsync(),
                _currentUser.Roles, comment, HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers["User-Agent"].ToString());

            ncr.Status = decision == "Approve" ? "Dispositioned" : "Open";
            if (decision != "Approve")
            {
                // رد شد - Disposition قبلی دیگر معتبر نیست، باید دوباره بررسی شود
                ncr.Disposition = null;
            }

            await _context.SaveChangesAsync();

            TempData["Message"] = decision == "Approve" ? "Disposition تایید شد" : "Disposition رد شد - NCR دوباره باز شد";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(NcrDetails), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NcrClose(int id)
    {
        var ncr = await _context.NonConformanceReports.FirstOrDefaultAsync(x => x.NonConformanceReportId == id);
        if (ncr == null) return NotFound();

        if (ncr.Status != "Dispositioned")
        {
            TempData["Error"] = "قبل از بستن NCR باید Disposition ثبت شده باشد.";
            return RedirectToAction(nameof(NcrDetails), new { id });
        }

        ncr.Status = "Closed";
        ncr.ClosedById = await GetCurrentEmployeeIdAsync();
        ncr.ClosedDate = DateTime.Now;

        await _context.SaveChangesAsync();

        TempData["Message"] = "NCR closed";
        return RedirectToAction(nameof(NcrDetails), new { id });
    }


    // =========================
    // KPI Report
    // =========================

    public async Task<IActionResult> Kpi()
    {
        ViewData["Title"] = "QC/QA KPI Report";
        ViewData["Section"] = "QC/QA";

        var ncrs = await _context.NonConformanceReports
            .Where(x => x.IsActive)
            .Include(x => x.QCDiscipline)
            .ToListAsync();

        // نرخ NCR به تفکیک دیسیپلین
        var byDiscipline = ncrs
            .GroupBy(x => x.QCDiscipline?.Name ?? "نامشخص")
            .Select(g => new
            {
                Discipline = g.Key,
                Total = g.Count(),
                Open = g.Count(x => x.Status != "Closed"),
                Critical = g.Count(x => x.Severity == "Critical"),
                AvgDaysToClose = g.Where(x => x.ClosedDate.HasValue)
                    .Select(x => (x.ClosedDate!.Value - x.RaisedDate).TotalDays)
                    .DefaultIfEmpty(0)
                    .Average()
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        // نرخ NCR به تفکیک شدت
        var bySeverity = ncrs
            .GroupBy(x => x.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToList();

        // نرخ NCR به تفکیک ماه (۱۲ ماه اخیر)
        var byMonth = ncrs
            .Where(x => x.RaisedDate >= DateTime.Now.AddMonths(-12))
            .GroupBy(x => x.RaisedDate.ToString("yyyy-MM"))
            .Select(g => new { Month = g.Key, Count = g.Count() })
            .OrderBy(x => x.Month)
            .ToList();

        ViewBag.ByDiscipline = byDiscipline;
        ViewBag.BySeverity = bySeverity;
        ViewBag.ByMonth = byMonth;
        ViewBag.TotalNCRs = ncrs.Count;
        ViewBag.ClosedOnTimeRate = ncrs.Count(x => x.ClosedDate.HasValue) == 0
            ? 0
            : Math.Round(100.0 * ncrs.Count(x => x.ClosedDate.HasValue && (x.ClosedDate.Value - x.RaisedDate).TotalDays <= 14)
                / ncrs.Count(x => x.ClosedDate.HasValue), 1);

        return View();
    }


    // =========================
    // Helpers
    // =========================

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;

        return await _context.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();
    }

    private async Task LoadCommonDropdownsAsync()
    {
        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.QCDisciplines = await _context.QCDisciplines.Where(x => x.IsActive)
            .OrderBy(x => x.Name).ToListAsync();

        ViewBag.Equipments = await _context.Equipments.Where(x => x.IsActive)
            .OrderBy(x => x.TagNumber)
            .Take(300)
            .Select(x => new { x.EquipmentId, x.TagNumber, x.Name })
            .ToListAsync();

        // طبق ممیزی نهایی Ownership (§3/§13): پیش از این، فیلدهای ProcurementPackageId/ContractId
        // روی InspectionTestPlan (از Phase 5) هیچ‌جای UI ساخت ITP در دسترس نبودند - ستون‌ها
        // وجود داشتند ولی هیچ کاربری نمی‌توانست آن‌ها را واقعاً پر کند.
        ViewBag.ProcurementPackages = await _context.ProcurementPackages.Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new { x.ProcurementPackageId, x.PackageNumber })
            .ToListAsync();

        ViewBag.Contracts = await _context.Contracts.Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new { x.ContractId, x.ContractNo })
            .ToListAsync();
    }
}
