using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Context;
using PEMS.Web.Models;
using PEMS.WorkflowPlatform.Interfaces;
using System.Diagnostics;

namespace PEMS.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly PEMSDbContext _context;
        private readonly ILogger<HomeController> _logger;
        private readonly PEMS.Application.Interfaces.IUserDashboardService _dashboardService;
        private readonly PEMS.Web.Security.ICurrentUserService _currentUser;
        private readonly PEMS.Application.Interfaces.IRoleModuleActionService _roleModuleActionService;
        private readonly PEMS.Application.Interfaces.IPermissionService _permissionService;
        private readonly PEMS.Application.Interfaces.ISetupService _setupService;
        private readonly IWorkflowRuntime _runtime;


        public HomeController(PEMSDbContext context, ILogger<HomeController> logger,
            PEMS.Application.Interfaces.IUserDashboardService dashboardService,
            PEMS.Web.Security.ICurrentUserService currentUser,
            PEMS.Application.Interfaces.IRoleModuleActionService roleModuleActionService,
            PEMS.Application.Interfaces.IPermissionService permissionService,
            PEMS.Application.Interfaces.ISetupService setupService,
            IWorkflowRuntime runtime)
        {
            _context = context;
            _logger = logger;
            _dashboardService = dashboardService;
            _currentUser = currentUser;
            _roleModuleActionService = roleModuleActionService;
            _permissionService = permissionService;
            _setupService = setupService;
            _runtime = runtime;
        }


        public async Task<IActionResult> Index()
        {
            // اگر Migration جدید هنوز روی دیتابیس اعمال نشده باشد، برخی جداول ممکن است وجود نداشته باشند.
            // به‌جای کرش کردن کل Dashboard، نام جدول‌های مشکل‌دار اینجا جمع‌آوری و به کاربر هشدار داده می‌شود.
            var failedTables = new List<string>();

            var model = new DashboardViewModel
            {
                ProjectsCount = await SafeCountAsync(() => _context.Projects.CountAsync(x => x.IsActive), "Projects", failedTables),

                DocumentsCount = await SafeCountAsync(() => _context.EngineeringDocuments.CountAsync(), "EngineeringDocuments", failedTables),

                PendingApprovalCount = await SafeCountAsync(() => _context.EngineeringDocuments
                    .CountAsync(x => x.Status == "Submitted" || x.Status == "Checked"), "EngineeringDocuments", failedTables),

                RejectedCount = await SafeCountAsync(() => _context.EngineeringDocuments
                    .CountAsync(x => x.Status == "Rejected"), "EngineeringDocuments", failedTables),

                OpenRevisionsCount = await SafeCountAsync(() => _context.DocumentRevisions
                    .CountAsync(x => x.IsActive && x.Status != "Approved"), "DocumentRevisions", failedTables),

                OpenWorkOrdersCount = await SafeCountAsync(() => _context.WorkOrders
                    .CountAsync(x => x.IsActive && x.Status != "Closed" && x.Status != "Completed"), "WorkOrders", failedTables),

                AIAlertsCount = await SafeCountAsync(() => _context.DocumentAttachments
                    .CountAsync(x => x.AIStatus == "Failed"), "DocumentAttachments", failedTables),

                EquipmentCount = await SafeCountAsync(() => _context.Equipments.CountAsync(), "Equipments", failedTables),

                OpenMRCount = await SafeCountAsync(() => _context.MaterialRequisitions
                    .CountAsync(x => x.IsActive && x.Status != "Closed" && x.Status != "Cancelled"), "MaterialRequisitions", failedTables),

                TransmittalsCount = await SafeCountAsync(() => _context.Transmittals.CountAsync(x => x.IsActive), "Transmittals", failedTables),
            };


            try
            {
                model.DocumentsByStatus = await _context.EngineeringDocuments
                    .GroupBy(x => x.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Status, x => x.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "خواندن آمار وضعیت مدارک برای Dashboard ناموفق بود");
                model.DocumentsByStatus = new Dictionary<string, int>();
                failedTables.Add("EngineeringDocuments (status breakdown)");
            }


            if (failedTables.Count > 0)
            {
                ViewBag.DashboardWarning =
                    "برخی از جداول جدید هنوز در دیتابیس ایجاد نشده‌اند (Migration اعمال نشده است): " +
                    string.Join(", ", failedTables.Distinct()) +
                    ". لطفاً این دستورات را اجرا کنید: " +
                    "dotnet ef migrations add AddEngineeringAndAIModules --project PEMS.Persistence --startup-project PEMS.Web " +
                    "سپس dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web";
            }


            // ===== Personalized Dashboard Widgets (طبق بازبینی معماری، بند ۲) =====
            // این بخش کاملاً افزودنی است و به منطق بالای همین متد کاری ندارد.
            if (_currentUser.UserId.HasValue)
            {
                var widgetLayout = await _dashboardService.GetLayoutAsync(_currentUser.UserId.Value);
                ViewBag.WidgetLayout = widgetLayout.Where(x => x.IsVisible).OrderBy(x => x.DisplayOrder).ToList();

                var widgetData = new Dictionary<string, object>();

                foreach (var w in (List<PEMS.Application.DTOs.UserDashboardWidgetDto>)ViewBag.WidgetLayout)
                {
                    try
                    {
                        widgetData[w.WidgetKey] = w.WidgetKey switch
                        {
                            "PendingDocuments" => await _context.EngineeringDocuments.CountAsync(x => x.IsActive && (x.Status == "Submitted" || x.Status == "Checked")),
                            "OverdueLetters" => await _context.LetterReferrals.CountAsync(x => x.Status != "Completed" && x.DueDate.HasValue && x.DueDate.Value < DateTime.Now),
                            "RecentDocuments" => await _context.EngineeringDocuments.Where(x => x.IsActive).OrderByDescending(x => x.CreatedDate).Take(5)
                                .Select(x => x.DocumentNo + " - " + x.Title).ToListAsync(),
                            "VDRSummary" => await _context.VDRItems.CountAsync(x => x.IsActive && x.WorkflowStatus != "Closed"),
                            "IntakeSummary" => await _context.DocumentAttachments.CountAsync(x => x.IntakeStage != null && x.IntakeStage != "Routed" && x.IntakeStage != "Rejected"),
                            "MyReferrals" => await _context.LetterReferrals
                                .Where(r => r.Status != "Completed" && _context.Users.Any(u => u.Id == _currentUser.UserId && u.EmployeeId == r.ReferredToEmployeeId))
                                .CountAsync(),
                            _ => (object)0
                        };
                    }
                    catch
                    {
                        widgetData[w.WidgetKey] = 0;
                    }
                }

                ViewBag.WidgetData = widgetData;
            }
            else
            {
                ViewBag.WidgetLayout = new List<PEMS.Application.DTOs.UserDashboardWidgetDto>();
                ViewBag.WidgetData = new Dictionary<string, object>();
            }


            // ===== My Work (طبق PEMS-UI-04): "چه چیزی نیاز به اقدام من دارد" - افزودنی محض،
            // به منطق بالای همین متد کاری ندارد. هر شکست این‌جا نباید کل Dashboard را بشکند
            // (همون الگوی SafeCountAsync). =====
            try
            {
                if (_currentUser.UserId.HasValue)
                {
                    var employeeId = await _context.Users.Where(u => u.Id == _currentUser.UserId.Value)
                        .Select(u => u.EmployeeId).FirstOrDefaultAsync();

                    if (employeeId.HasValue)
                    {
                        var summary = new MyWorkSummary();

                        var canApprovePermits = await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Permit", "Approve")
                            || await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Permit", "Reject");
                        if (canApprovePermits)
                        {
                            summary.ShowPermitApprovals = true;
                            var pendingTasks = await _runtime.GetPendingTasksAsync(employeeId.Value, _currentUser.Roles);
                            summary.PendingPermitApprovals = pendingTasks.Count(t => t.BusinessObjectType == "Permit");
                        }

                        if (await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "View"))
                        {
                            summary.ShowWorkOrders = true;
                            summary.MyWorkOrdersCount = await _context.WorkOrders.CountAsync(x =>
                                x.IsActive && x.AssignedEmployeeId == employeeId.Value && x.Status != "Closed" && x.Status != "Completed");
                        }

                        summary.ShowReferrals = true;
                        summary.MyReferralsCount = await _context.LetterReferrals.CountAsync(r =>
                            r.Status != "Completed" && r.ReferredToEmployeeId == employeeId.Value);

                        ViewBag.MyWork = summary;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "محاسبه‌ی بخش My Work برای Dashboard ناموفق بود");
                ViewBag.MyWork = null;
            }


            // ===== Setup Status Card (طبق «PEMS — Quick Setup & Domain Import Architecture
            // Refactoring» بند ۱۴): افزودنی محض، به منطق بالای همین متد کاری ندارد. فقط برای
            // کاربرانی نشان داده می‌شود که مجوز Setup/View دارند - نه هر کاربر احرازهویت‌شده‌ای،
            // چون این اطلاعات (وضعیت آمادگی کل سیستم) برای نقش‌های عملیاتی معنادار نیست. =====
            try
            {
                if (await _permissionService.HasPermissionAsync(_currentUser.Roles, "Setup", "View"))
                {
                    ViewBag.SetupStatus = await _setupService.GetStatusAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "محاسبه‌ی وضعیت Setup برای Dashboard ناموفق بود");
                ViewBag.SetupStatus = null;
            }


            // ===== ساخت لینک‌های سریع برای KPI کارت‌ها =====
            model.KpiCards = new List<KpiCardViewModel>
            {
                new KpiCardViewModel { Title = "Active Projects", Count = model.ProjectsCount, IconClass = "bi-kanban", ColorClass = "bg-icon-blue", Url = Url.Action("Index", "Projects") },
                new KpiCardViewModel { Title = "Engineering Documents", Count = model.DocumentsCount, IconClass = "bi-file-earmark-text", ColorClass = "bg-icon-slate", Url = Url.Action("Index", "EngineeringDocuments") },
                new KpiCardViewModel { Title = "Pending Approval", Count = model.PendingApprovalCount, IconClass = "bi-hourglass-split", ColorClass = "bg-icon-orange", Url = Url.Action("Index", "EngineeringDocuments", new { status = "Submitted,Checked" }) },
                new KpiCardViewModel { Title = "Rejected Documents", Count = model.RejectedCount, IconClass = "bi-x-circle", ColorClass = "bg-icon-red", Url = Url.Action("Index", "EngineeringDocuments", new { status = "Rejected" }) },
                new KpiCardViewModel { Title = "Open Revisions", Count = model.OpenRevisionsCount, IconClass = "bi-clock-history", ColorClass = "bg-icon-purple", Url = Url.Action("Index", "DocumentRevisions") },
                new KpiCardViewModel { Title = "Open MR / MRQ", Count = model.OpenMRCount, IconClass = "bi-cart-check", ColorClass = "bg-icon-teal", Url = Url.Action("Index", "MaterialRequisition") },
                
                // این کارت چون Coming Soon هست، Url ندارد
                new KpiCardViewModel { Title = "Active Contracts (coming soon)", Count = 0, IconClass = "bi-file-earmark-ruled", ColorClass = "bg-icon-slate", Url = null },

                new KpiCardViewModel { Title = "Open Work Orders (CMMS)", Count = model.OpenWorkOrdersCount, IconClass = "bi-clipboard-check", ColorClass = "bg-icon-teal", Url = Url.Action("Index", "WorkOrder") },
                new KpiCardViewModel { Title = "AI Alerts (Failed Analysis)", Count = model.AIAlertsCount, IconClass = "bi-robot", ColorClass = "bg-icon-red", Url = Url.Action("Index", "Intake") },
                new KpiCardViewModel { Title = "Registered Equipment", Count = model.EquipmentCount, IconClass = "bi-gear", ColorClass = "bg-icon-green", Url = Url.Action("Index", "Equipment") },
                new KpiCardViewModel { Title = "Transmittals", Count = model.TransmittalsCount, IconClass = "bi-send", ColorClass = "bg-icon-purple", Url = Url.Action("Index", "Transmittal") }
            };


            return View(model);
        }


        // اجرای امن یک کوئری شمارش: اگر جدول مربوطه هنوز در دیتابیس وجود نداشته باشد
        // (مثلاً چون Migration جدید هنوز اعمال نشده)، به‌جای کرش کل صفحه، صفر برمی‌گرداند
        // و نام جدول را برای نمایش هشدار در Dashboard ثبت می‌کند.
        private async Task<int> SafeCountAsync(Func<Task<int>> query, string tableNameForLog, List<string> failedTables)
        {
            try
            {
                return await query();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "خواندن آمار جدول {Table} برای Dashboard ناموفق بود (احتمالاً Migration اعمال نشده)", tableNameForLog);
                failedTables.Add(tableNameForLog);
                return 0;
            }
        }


        public IActionResult Privacy()
        {
            return View();
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}