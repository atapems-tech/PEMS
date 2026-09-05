using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Models;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق Phase E-05D: مصرف‌کننده‌ی قرارداد Query تایید‌شده در E-05C - بدون هیچ Query جدید یا
/// منطق کسب‌وکار تازه. فقط دو متد واقعی امروز موجودند (GetSummaryCountsAsync از E-03،
/// GetRevisionQualityAsync از E-05C) - بخش‌های Discipline Performance/Workflow Bottleneck/
/// Vendor Performance که در E-05B طراحی شدند اما هرگز پیاده نشدند، این‌جا ساخته نمی‌شوند
/// (طبق قانون KPI Contract: عدم جعل مقدار برای Query ای که وجود ندارد) - در گزارش E-05D
/// به‌عنوان نیاز Backend آینده مستند شده‌اند.
/// </summary>
public class EngineeringDashboardController : Controller
{
    private readonly IEngineeringDocumentService _service;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly PEMS.Application.Interfaces.IAccessControlService _accessControlService;

    public EngineeringDashboardController(
        IEngineeringDocumentService service, PEMSDbContext context, ICurrentUserService currentUser,
        PEMS.Application.Interfaces.IAccessControlService accessControlService)
    {
        _service = service;
        _context = context;
        _currentUser = currentUser;
        _accessControlService = accessControlService;
    }

    public async Task<IActionResult> Index(int? projectId, int? disciplineId, int? minRevisionCount)
    {
        ViewData["Title"] = "Engineering Dashboard";
        ViewData["Section"] = "Engineering Management";

        // طبق Phase E-05D §15 (دقیقاً همون دو سیگنال EngineeringDocumentsController.Index، E-03).
        // طبق فاز ۳ RBAC-ABAC: آرایه‌ی ثابت UnrestrictedRoles جایگزین شد با همون مجوز داده‌محور
        // «EngineeringDocuments.ViewAllDisciplines» که EngineeringDocumentsController می‌خواند -
        // دیگر کپی جداگانه در این فایل نیست، پس نمی‌تواند از آن Drift کند.
        var canViewAllDisciplines = (await _accessControlService.AuthorizeForRolesAsync(
            _currentUser.Roles, "EngineeringDocuments", "ViewAllDisciplines")).Allowed;
        var isRestricted = _currentUser.UserId.HasValue && !canViewAllDisciplines;
        int? myDisciplineId = null;
        if (isRestricted)
        {
            var employeeId = await GetCurrentEmployeeIdAsync();
            if (!employeeId.HasValue)
            {
                isRestricted = false;
            }
            else
            {
                var employee = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId.Value);
                if (employee == null) isRestricted = false;
                else myDisciplineId = employee.DisciplineId;
            }
        }

        // انتخاب دیسیپلین از فیلتر داشبورد روی محدودیت امنیتی سوار می‌شود، هرگز جایگزینش نمی‌شود:
        // کاربر نامحدود که یک دیسیپلین انتخاب می‌کند، از همون مسیر کد محدودیت رد می‌شود (یک
        // فیلتر داوطلبانه)؛ کاربر محدود اگر دیسیپلین دیگری بخواهد وارد کند، نادیده گرفته می‌شود.
        var effectiveRestricted = isRestricted || disciplineId.HasValue;
        var effectiveDisciplineId = isRestricted ? myDisciplineId : disciplineId;

        var summary = await _service.GetSummaryCountsAsync(isRestricted, myDisciplineId);
        var quality = await _service.GetRevisionQualityAsync(effectiveRestricted, effectiveDisciplineId, minRevisionCount, projectId);

        var withKnownOutcome = quality.Where(x => x.FirstPassApproved.HasValue).ToList();
        var attentionQueue = BuildAttentionQueue(quality);
        var disciplinePerformance = BuildDisciplinePerformance(quality);
        var healthScore = CalculateEngineeringHealthScore(summary, quality);

        var model = new EngineeringDashboardViewModel
        {
            Summary = summary,
            Quality = quality,
            // طبق قانون "Missing نباید Zero جا زده شود" (E-05A/E-05C): وقتی هیچ سندی هنوز یک
            // چرخه را کامل نکرده، نرخ null است - نه صفر.
            FirstPassApprovalRate = withKnownOutcome.Count > 0
                ? (double?)withKnownOutcome.Count(x => x.FirstPassApproved == true) / withKnownOutcome.Count
                : null,
            EngineeringHealthScore = healthScore,
            AttentionQueue = attentionQueue,
            DisciplinePerformance = disciplinePerformance,
            SelectedProjectId = projectId,
            SelectedDisciplineId = disciplineId,
            SelectedMinRevisionCount = minRevisionCount
        };

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive).OrderBy(x => x.ProjectName)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();
        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();

        return View(model);
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;

        return await _context.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();
    }

    private static int CalculateEngineeringHealthScore(EngineeringDocumentSummaryCountsDto summary, IReadOnlyCollection<EngineeringRevisionQualityDto> quality)
    {
        var total = Math.Max(summary.Total, 1);
        var approvedRatio = summary.Total > 0 ? (double)summary.Approved / total : 0d;
        var pendingRatio = summary.Total > 0 ? (double)summary.Pending / total : 0d;
        var overdueRatio = summary.Total > 0 ? (double)summary.Overdue / total : 0d;
        var riskRatio = quality.Count > 0
            ? (double)quality.Count(x => x.RejectionCount > 0 || x.RevisionCount >= 2) / quality.Count
            : 0d;

        var weightedScore = (approvedRatio * 0.45)
            + ((1d - pendingRatio) * 0.2)
            + ((1d - overdueRatio) * 0.2)
            + ((1d - riskRatio) * 0.15);

        return Math.Clamp((int)Math.Round(weightedScore * 100d), 0, 100);
    }

    private static List<EngineeringAttentionQueueItem> BuildAttentionQueue(IReadOnlyCollection<EngineeringRevisionQualityDto> quality)
    {
        return quality
            .Where(x => x.RejectionCount > 0 || x.RevisionCount >= 2)
            .OrderByDescending(x => x.RejectionCount)
            .ThenByDescending(x => x.RevisionCount)
            .Take(6)
            .Select(x => new EngineeringAttentionQueueItem
            {
                DocumentId = x.DocumentId,
                DocumentNo = x.DocumentNo,
                DisciplineName = string.IsNullOrWhiteSpace(x.DisciplineName) ? "-" : x.DisciplineName,
                Reason = x.RejectionCount > 0 ? $"{x.RejectionCount} rejection(s)" : "Revision pressure",
                Severity = x.RejectionCount > 2 || x.RevisionCount >= 4 ? "Critical" : x.RejectionCount > 0 ? "High" : "Medium",
                RevisionCount = x.RevisionCount,
                RejectionCount = x.RejectionCount
            })
            .ToList();
    }

    private static List<EngineeringDisciplinePerformanceItem> BuildDisciplinePerformance(IReadOnlyCollection<EngineeringRevisionQualityDto> quality)
    {
        return quality
            .Where(x => !string.IsNullOrWhiteSpace(x.DisciplineName))
            .GroupBy(x => x.DisciplineName)
            .Select(g => new EngineeringDisciplinePerformanceItem
            {
                DisciplineName = g.Key ?? "-",
                DocumentCount = g.Count(),
                RejectionCount = g.Sum(x => x.RejectionCount),
                RevisionCount = g.Sum(x => x.RevisionCount),
                RejectionRate = g.Count() > 0 ? (double)g.Count(x => x.RejectionCount > 0) / g.Count() : 0d,
                RiskScore = Math.Clamp(
                    (int)Math.Round(
                        ((double)g.Count(x => x.RejectionCount > 0) / Math.Max(g.Count(), 1)) * 55d +
                        ((double)g.Sum(x => x.RevisionCount) / Math.Max(g.Count(), 1)) * 10d),
                    0,
                    100)
            })
            .OrderByDescending(x => x.RiskScore)
            .ThenByDescending(x => x.DocumentCount)
            .Take(8)
            .ToList();
    }
}
