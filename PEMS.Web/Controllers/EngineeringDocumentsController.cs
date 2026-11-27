using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Web.Controllers;


public class EngineeringDocumentsController : Controller
{
    private readonly IEngineeringDocumentService _service;
    private readonly PEMSDbContext _context;
    private readonly IDocumentWorkflowService _workflowService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PEMS.Application.Interfaces.ILetterService _letterService;
    private readonly PEMS.Application.Interfaces.IDocumentNumberingService _numberingService;
    private readonly PEMS.Application.Interfaces.IPermissionService _permissionService;
    private readonly PEMS.Web.Security.ICurrentUserService _currentUser;
    private readonly PEMS.Application.Interfaces.IDocumentCommentService _commentService;
    private readonly PEMS.Application.Interfaces.IEngineeringCopilotService _copilotService;
    private readonly PEMS.Application.Interfaces.IDocumentHoldService _holdService;
    private readonly PEMS.Application.Interfaces.IAccessControlService _accessControlService;
    private readonly PEMS.Application.Interfaces.IFileStorageService _fileStorage;
    private readonly PEMS.Application.Interfaces.ITransmittalService _transmittalService;
    private readonly ILogger<EngineeringDocumentsController> _logger;


    public EngineeringDocumentsController(
        IEngineeringDocumentService service,
        PEMSDbContext context,
        IDocumentWorkflowService workflowService,
        IServiceScopeFactory scopeFactory,
        PEMS.Application.Interfaces.ILetterService letterService,
        PEMS.Application.Interfaces.IDocumentNumberingService numberingService,
        PEMS.Application.Interfaces.IPermissionService permissionService,
        PEMS.Web.Security.ICurrentUserService currentUser,
        PEMS.Application.Interfaces.IDocumentCommentService commentService,
        PEMS.Application.Interfaces.IEngineeringCopilotService copilotService,
        PEMS.Application.Interfaces.IDocumentHoldService holdService,
        PEMS.Application.Interfaces.IAccessControlService accessControlService,
        PEMS.Application.Interfaces.IFileStorageService fileStorage,
        PEMS.Application.Interfaces.ITransmittalService transmittalService,
        ILogger<EngineeringDocumentsController> logger)
    {
        _service = service;
        _context = context;
        _workflowService = workflowService;
        _scopeFactory = scopeFactory;
        _letterService = letterService;
        _numberingService = numberingService;
        _permissionService = permissionService;
        _currentUser = currentUser;
        _commentService = commentService;
        _copilotService = copilotService;
        _holdService = holdService;
        _accessControlService = accessControlService;
        _fileStorage = fileStorage;
        _transmittalService = transmittalService;
        _logger = logger;
    }



    // =========================
    // LIST
    // =========================

    // محدودیت دیسیپلین فهرست - یک‌جا برای Index و Export تا Export هرگز بیش از آنچه کاربر در فهرست می‌بیند خارج نکند
    private async Task<(bool IsRestricted, int? MyDisciplineId)> ResolveListScopeAsync()
    {
        // طبق Phase E-03 (رفع P1 ممیزی Performance): دقیقاً همان دو سیگنال جدایی که
        // ScopeToOwnDisciplineIfRestrictedAsync امروز درون‌حافظه‌ای اعمال می‌کند - این‌جا
        // زودتر محاسبه و به لایه‌ی SQL پاس داده می‌شود. کارمند بدون دیسیپلین همچنان محدود به
        // مدارک بدون‌دیسیپلین می‌ماند (نه "نامحدود") - طبق رفتار دقیق فعلی.
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

        return (isRestricted, myDisciplineId);
    }

    // طبق دانش سیستم‌های EDMS معتبر (AVEVA Engineering / Aconex / Bentley ProjectWise):
    // محدودیت "فقط دیسیپلین خودم" یک قانون کورکورانه برای همه نیست - فقط روی نقش‌های اجرایی
    // (Discipline Engineer) اعمال می‌شود. نقش‌های نظارتی/تاییدکننده (Manager, Supervisor,
    // Document Controller, System Admin) باید همچنان کل MDR را ببینند، چون کارشان دقیقاً
    // بررسی کامل بودن مدارک بین‌رشته‌ای و تایید گردش‌کار است - محدودکردن آن‌ها هم، کارشان را
    // غیرممکن می‌کند. مدارک بدون دیسیپلین مشخص (Unassigned) هم از دید هیچ‌کس مخفی نمی‌شوند.
    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;

        return await _context.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();
    }

    // طبق فاز ۱ ممیزی گردش‌کار: برای Audit Trail واقعی (IP/Browser) طبق سند
    private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetUserAgent() => Request.Headers["User-Agent"].ToString();

    // طبق Phase E-01: تشخیص نقض ایندکس Unique شماره‌ی مدرک (DocumentNo) - همون منطق
    // NumberingService.IsUniqueConstraintViolation
    private static bool IsDuplicateDocumentNo(Microsoft.EntityFrameworkCore.DbUpdateException ex)
    {
        return ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx
            && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }

    private async Task<int?> GetCurrentEmployeeDisciplineIdAsync()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (!employeeId.HasValue) return null;

        return await _context.Employees
            .Where(e => e.EmployeeId == employeeId.Value)
            .Select(e => (int?)e.DisciplineId)
            .FirstOrDefaultAsync();
    }

    // طبق بررسی چرخه‌ی مدرک: دیدن مدارک و اجازه‌ی تایید دو چیز جدا هستند. مدیر مهندسی و ادمین
    // سیستم می‌توانند هر دیسیپلینی را تایید کنند؛ سرپرست فقط دیسیپلین خودش را (نه یک تاییدکننده‌ی
    // عمومی برای همه‌چیز) - این دقیقاً همان چیزی است که در پروژه‌های بزرگ EPC واقعی هم رعایت می‌شود.
    // طبق «PEMS Authorization Architecture Completion» فاز ۳: جایگزین داده‌محور سه نقش ثابت قبلی
    // (SystemAdmin/EngineeringManager/EngineeringSupervisor) - رفتار عیناً یکسان، اثبات‌شده با
    // این‌که RolePermission همین دو نقش غیر-Admin را از قبل با CanApprove=true داشت (SystemAdmin
    // از طریق Bypass عمومی AccessControlService عبور می‌کند، دقیقاً مثل قبل). «هر دیسیپلینی»
    // اکنون یک مجوز جدا (EngineeringDocuments.ApproveAllDisciplines، فقط برای Manager) است - نه
    // بازاستفاده از ViewAllDisciplines (که به سرپرست هم داده شده و دامنه‌ی تایید را غلط می‌گستراند).
    private async Task<bool> CanApproveDocumentDisciplineAsync(int? documentDisciplineId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "EngineeringDocuments", "Approve"))
            return false;

        var unrestricted = await _accessControlService.AuthorizeForRolesAsync(_currentUser.Roles, "EngineeringDocuments", "ApproveAllDisciplines");
        if (unrestricted.Allowed) return true;

        if (!documentDisciplineId.HasValue) return true; // مدرک بدون دیسیپلین مشخص، محدودیتی ندارد

        var myEmployeeId = await GetCurrentEmployeeIdAsync();
        if (!myEmployeeId.HasValue) return false;

        var myEmployee = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == myEmployeeId.Value);
        return myEmployee != null && myEmployee.DisciplineId == documentDisciplineId.Value;
    }

    // طبق سند RBAC-ABAC Authorization Architecture (فاز ۳ - جایگزینی مکانیزم D): آرایه‌ی ثابت
    // UnrestrictedRoles قبلی جایگزین شد با مجوز داده‌محور «EngineeringDocuments.ViewAllDisciplines»
    // (AccessControlSeeder.SeedPhase3PermissionsAsync) - رفتار عیناً یکسان (همان ۴ نقش،
    // اثبات‌شده با AccessControlPhase3ParityTests)، اما دیگر در سه فایل کپی نشده.
    private async Task<List<PEMS.Application.DTOs.EngineeringDocumentDto>> ScopeToOwnDisciplineIfRestrictedAsync(
        List<PEMS.Application.DTOs.EngineeringDocumentDto> documents)
    {
        var canViewAllDisciplines = (await _accessControlService.AuthorizeForRolesAsync(
            _currentUser.Roles, "EngineeringDocuments", "ViewAllDisciplines")).Allowed;
        if (canViewAllDisciplines)
            return documents;

        if (!_currentUser.UserId.HasValue)
            return documents;

        var myEmployeeId = await _context.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();

        if (!myEmployeeId.HasValue)
            return documents;

        var myEmployee = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == myEmployeeId.Value);

        if (myEmployee == null)
            return documents;

        return documents
            .Where(x => x.DisciplineId == myEmployee.DisciplineId || x.DisciplineId == 0)
            .ToList();
    }





    // =========================
    // CREATE GET
    // =========================

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        LoadDropdowns();

        ViewBag.CanManualNumber = await _permissionService.HasPermissionAsync(_currentUser.Roles, "EngineeringDocuments", "ManualNumber");

        return View();
    }





    // =========================
    // CREATE POST
    // =========================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        EngineeringDocument document, bool useManualNumber = false)
    {
        // طبق «PEMS EDMS Phase 4 Audit» یافته P4-01: پیش از این هیچ بررسی مجوز روی ایجاد مدرک
        // وجود نداشت - هر کاربر احرازهویت‌شده (با هر نقشی) می‌توانست مدرک بسازد. دقیقاً همان
        // الگوی Freeze/Unfreeze/Hold همین کنترلر.
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "EngineeringDocuments", "Create"))
            return Forbid();

        if (!ModelState.IsValid)
        {
            LoadDropdowns();
            return View(document);
        }



        document.Status = "Draft";

        document.RevisionNo = "00";

        // طبق بررسی چرخه‌ی مدرک: کسی که مدرک را ایجاد می‌کند، همان "Prepared By" واقعی است -
        // نه یک فیلد متنی که بعداً دستی پر شود
        var creatorEmployeeId = await GetCurrentEmployeeIdAsync();
        document.PreparedById = creatorEmployeeId;

        if (creatorEmployeeId.HasValue)
        {
            var creator = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == creatorEmployeeId.Value);
            if (creator != null) document.PreparedBy = creator.FirstName + " " + creator.LastName;
        }

        // شماره‌گذاری خودکار پیش‌فرض؛ فقط اگر کاربر مجوز CanManualNumber داشته باشد و صریحاً
        // شماره‌ی دستی وارد کرده باشد، همان حفظ می‌شود - طبق بند ۱۰ بازبینی معماری
        var canManualNumber = await _permissionService.HasPermissionAsync(_currentUser.Roles, "EngineeringDocuments", "ManualNumber");

        if (!useManualNumber || !canManualNumber || string.IsNullOrWhiteSpace(document.DocumentNo))
        {
            document.DocumentNo = await _numberingService.GenerateNextNumberAsync(
                document.ProjectId, document.DisciplineId, document.DocumentType);
        }

        // ایجاد مدرک اصلی
        // طبق Phase E-01 (رفع P0 ممیزی): شماره‌ی خودکار حالا اتمیک است، ولی شماره‌ی دستی هنوز
        // می‌تواند با یک مدرک دیگر تصادفی یکی باشد - ایندکس Unique سطح دیتابیس این را رد می‌کند؛
        // این‌جا فقط پیام دوستانه به‌جای خطای خام SQL نشان داده می‌شود.
        try
        {
            await _service.AddAsync(document);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (IsDuplicateDocumentNo(ex))
        {
            LoadDropdowns();
            ModelState.AddModelError(nameof(document.DocumentNo), "این شماره‌ی مدرک قبلاً استفاده شده است.");
            return View(document);
        }





        // =========================
        // Initial Revision
        // =========================


        var revision = new DocumentRevision
        {
            DocumentId = document.DocumentId,

            RevisionNo = "00",

            Status = "Draft",

            Comment = "Initial Revision",

            RevisionDate = DateTime.Now,

            IsActive = true
        };


        await _context.DocumentRevisions
            .AddAsync(revision);






        // =========================
        // Initial Workflow
        // =========================


        var workflow = new DocumentWorkflow
        {
            DocumentId = document.DocumentId,

            FromStatus = "",

            ToStatus = "Created",

            ActionType = "Create",

            Comment = "Document Created",

            ActionDate = DateTime.Now,

            IsActive = true
        };


        await _context.DocumentWorkflows
            .AddAsync(workflow);




        await _context.SaveChangesAsync();

        // طبق یافته‌ی P4-01: ثبت این اقدام در تاریخچه‌ی مجوزدهی - همان رکوردی که بعداً هنگام
        // Approve توسط AuthorizeAsync برای تشخیص خودتاییدی (SoD) خوانده می‌شود
        if (_currentUser.UserId.HasValue)
        {
            await _accessControlService.AuthorizeAsync(new PEMS.Application.DTOs.AccessControlRequest
            {
                UserId = _currentUser.UserId.Value,
                ResourceName = "EngineeringDocuments",
                ActionName = "Create",
                ResourceId = document.DocumentId.ToString(),
                IpAddress = GetClientIp()
            });
        }

        return Redirect("/p/eng-docs");

    }






    // =========================
    // DETAILS
    // =========================


    public async Task<IActionResult> Details(int id)
    {
        ViewData["HelpKey"] = "EngineeringDocuments.Details";

        var document =
            await _service.GetByIdAsync(id);


        if (document == null)
            return NotFound();

        // جلوگیری از دور زدن محدودیت دیسیپلین با ورود مستقیم به آدرس - نه فقط پنهان‌کردن از لیست
        var visibleCheck = await ScopeToOwnDisciplineIfRestrictedAsync(new List<PEMS.Application.DTOs.EngineeringDocumentDto> { document });
        if (!visibleCheck.Any())
            return Forbid();

        ViewBag.RelatedLetters = await _letterService.GetRelatedToDocumentAsync(id);

        // طبق «PEMS Phase 2A — EDMS Foundation» بخش ۲: پیوند متقابل با Transmittal (قبلاً هیچ‌جا
        // روی این صفحه نشان داده نمی‌شد - سرویس/موجودیت موجود، فقط یک Query هدفمند جدید)
        ViewBag.RelatedTransmittals = await _transmittalService.GetForDocumentAsync(id);

        // طبق Phase E-01
        ViewBag.CanFreeze = await _permissionService.HasPermissionAsync(_currentUser.Roles, "EngineeringDocuments", "Freeze");
        ViewBag.CanUnfreeze = await _permissionService.HasPermissionAsync(_currentUser.Roles, "EngineeringDocuments", "Unfreeze");

        // طبق بخش M ممیزی سخت‌سازی مهندسی
        ViewBag.CanHold = await _permissionService.HasPermissionAsync(_currentUser.Roles, "EngineeringDocuments", "Hold");
        ViewBag.CanReleaseHold = await _permissionService.HasPermissionAsync(_currentUser.Roles, "EngineeringDocuments", "ReleaseHold");
        ViewBag.HoldHistory = await _holdService.GetHistoryAsync(id);

        // طبق درخواست: چرخه‌ی کامنت/ریپلای دقیقاً مثل VDR
        ViewBag.Comments = await _commentService.GetForDocumentAsync(id);

        // برای پاپ‌آپ Submit (انتخاب دیسیپلین‌های IDC) و نمایش وضعیت لحظه‌ای IDC
        ViewBag.AllDisciplines = await _context.Disciplines.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        ViewBag.IdcStatus = await _workflowService.GetIdcStatusAsync(id);
        ViewBag.MyDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();

        // طبق «PEMS Engineering Workflow History Read-Path Correction»: Model.Workflows
        // (EngineeringDocument.Workflows) جدول قدیمی DocumentWorkflow است که فقط موقع Create
        // یک ردیف می‌گیرد - منبع واقعیِ تاریخچه‌ی کامل گردش‌کار WorkflowHistory است
        ViewBag.WorkflowHistory = await _workflowService.GetWorkflowHistoryAsync(id);

        // برای پاپ‌آپ Issue (انتخاب واحد تحویل داخلی) و پاپ‌آپ پاسخ کارفرما (انتخاب نامه‌ی وارده)
        ViewBag.AllDepartments = await _context.Departments.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        ViewBag.AvailableIncomingLetters = await _context.Letters
            .Where(l => l.IsActive && l.LetterType == "Incoming" && (l.RelatedDocumentId == null || l.RelatedDocumentId == id))
            .OrderByDescending(l => l.LetterDate)
            .Take(30)
            .ToListAsync();

        // ==============================================================================
        // طبق درخواست: کلیک روی شماره P&ID از تجهیز به این مدرک منتقل می‌شود، و برعکس. طبق
        // رفع باگ گزارش‌شده (پسوند رویژن اضافه در انتهای شماره‌ی ثبت‌شده روی تجهیز)، از همون
        // DocumentNumberMatcher سه‌لایه استفاده می‌شود که برای جهت رفت هم استفاده شد - این‌جا
        // چون ممکنه چند تجهیز مختلف به یک مدرک ارجاع بدن، همه‌ی نامزدهای منطبق نگه داشته می‌شن.
        // ==============================================================================
        var equipmentCandidates = await _context.Equipments
            .Where(x => x.IsActive && x.PIDNumber != null && x.ProjectId == document.ProjectId)
            .Select(x => new { x.EquipmentId, x.TagNumber, x.Name, x.PIDNumber })
            .ToListAsync();

        ViewBag.EquipmentUsingThisAsPID = equipmentCandidates
            .Where(x => PEMS.Shared.Search.DocumentNumberMatcher.IsMatch(x.PIDNumber, document.DocumentNo))
            .Select(x => new { x.EquipmentId, x.TagNumber, x.Name })
            .ToList();

        return View(document);

    }


    // طبق فاز ۴ نقشه‌راه معماری: کشف رابطه‌ی معنایی این مدرک با بقیه، به کمک همون زیرساخت
    // Semantic Search موجود - نه پیاده‌سازی جدید
    // =========================
    // EDIT GET
    // =========================


    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        // طبق یافته‌ی P4-01: همان بررسی سمت‌سرور - نمایش فرم ویرایش هم باید مجوز داشته باشد
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "EngineeringDocuments", "Edit"))
            return Forbid();

        var document =
            await _service.GetEntityByIdAsync(id);


        if (document == null)
            return NotFound();


        LoadDropdowns();


        return View(document);

    }





    // =========================
    // EDIT POST
    // =========================


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        EngineeringDocument document)
    {
        // طبق یافته‌ی P4-01
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "EngineeringDocuments", "Edit"))
            return Forbid();

        if (!ModelState.IsValid)
        {
            LoadDropdowns();
            return View(document);
        }


        document.ModifiedDate = DateTime.Now;


        try
        {
            await _service.UpdateAsync(document);
        }
        catch (PEMS.Application.Exceptions.DocumentFrozenException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id = document.DocumentId });
        }

        // طبق یافته‌ی P4-01: ثبت در تاریخچه‌ی مجوزدهی - Edit هم طبق SoDPolicy موجود با Approve
        // در تعارض است (کاربری که ویرایش کرده نمی‌تواند همان مدرک را تایید کند)
        if (_currentUser.UserId.HasValue)
        {
            await _accessControlService.AuthorizeAsync(new PEMS.Application.DTOs.AccessControlRequest
            {
                UserId = _currentUser.UserId.Value,
                ResourceName = "EngineeringDocuments",
                ActionName = "Edit",
                ResourceId = document.DocumentId.ToString(),
                IpAddress = GetClientIp()
            });
        }

        TempData["Message"] = "Document updated successfully";
        return RedirectToAction(nameof(Details), new { id = document.DocumentId });

    }





    // =========================
    // DELETE DOCUMENT
    // =========================


        // =========================
    // DROPDOWNS
    // =========================

    private void LoadDropdowns()
    {
        ViewBag.Projects =
            new SelectList(
                _context.Projects,
                "ProjectId",
                "ProjectName"
            );


        ViewBag.Disciplines =
            new SelectList(
                _context.Disciplines,
                "DisciplineId",
                "Name"
            );


        ViewBag.Equipments =
            new SelectList(
                _context.Equipments,
                "EquipmentId",
                "TagNumber"
            );


        ViewBag.Contracts =
            new SelectList(
                _context.Contracts,
                "ContractId",
                "ContractNo"
            );

        ViewBag.MaterialRequisitions =
            new SelectList(
                _context.MaterialRequisitions,
                "MRId",
                "MRNumber"
            );
    }





    // =========================
    // UPLOAD ATTACHMENT GET
    // =========================

    [HttpGet]
    public async Task<IActionResult> UploadAttachment(int id)
    {
        ViewBag.DocumentId = id;

        // طبق یافته F-06 ممیزی EDMS: کاربر باید بتواند صریحاً انتخاب کند این فایل برای کدام
        // رویژن است - اختیاری می‌ماند (گزینه‌ی "سطح مدرک، بدون رویژن مشخص" همیشه موجود است) تا
        // با جریان قدیمی/پیوست‌های عمومی سازگار بماند
        ViewBag.Revisions = await _context.DocumentRevisions
            .Where(x => x.DocumentId == id && x.IsActive)
            .OrderByDescending(x => x.RevisionId)
            .Select(x => new { x.RevisionId, x.RevisionNo, x.Status })
            .ToListAsync();

        return View();
    }





    // =========================
    // UPLOAD ATTACHMENT POST
    // =========================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PEMS.Web.Security.Uploads.UploadPolicy(PEMS.Application.Uploads.UploadProfile.Attachment)]
    public async Task<IActionResult> UploadAttachment(
        int documentId,
        IFormFile file,
        int? revisionId,
        string? fileRole)
    {

        if(file == null || file.Length == 0)
        {
            ModelState.AddModelError(
                "",
                "Please select a file"
            );

            ViewBag.DocumentId = documentId;
            ViewBag.Revisions = await _context.DocumentRevisions
                .Where(x => x.DocumentId == documentId && x.IsActive)
                .OrderByDescending(x => x.RevisionId)
                .Select(x => new { x.RevisionId, x.RevisionNo, x.Status })
                .ToListAsync();

            return View();
        }

        // طبق یافته F-06 ممیزی EDMS: اگر revisionId داده شده، باید واقعاً متعلق به همین مدرک باشد -
        // هرگز به یک رویژن مدرک دیگر وصل نمی‌شود
        if (revisionId.HasValue)
        {
            var revisionBelongsToDocument = await _context.DocumentRevisions
                .AnyAsync(x => x.RevisionId == revisionId.Value && x.DocumentId == documentId);
            if (!revisionBelongsToDocument)
                return BadRequest("رویژن انتخاب‌شده متعلق به این مدرک نیست.");
        }

        // طبق ممیزی امنیتی (P0 - Engineering File Security Containment): دیگر مستقیم زیر
        // wwwroot نوشته نمی‌شود - IFileStorageService ریشه‌ی امن بیرون از wwwroot را مدیریت می‌کند.
        // طبق یافته F-07 ممیزی EDMS: SHA-256 حین همان یک بار Streaming محاسبه می‌شود (نه با خواندن
        // دوباره‌ی فایل) - HashingStream همان کلاس ساخته‌شده برای Enterprise File Storage است،
        // این‌جا دوباره استفاده می‌شود نه بازتولید.
        string storageKey;
        string fileHash;
        await using (var uploadStream = file.OpenReadStream())
        {
            var hashingStream = new PEMS.Persistence.Services.HashingStream(uploadStream);
            storageKey = await _fileStorage.SaveAsync("documents", file.FileName, hashingStream);
            fileHash = hashingStream.GetHexHash();
        }

        // طبق یافته F-06: اگر این رویژن هنوز هیچ فایل رسمی ندارد، اولین فایل آپلودشده برایش
        // خودکار رسمی می‌شود؛ در غیر این صورت کاربر باید بعداً صریحاً "Set Official" کند - هرگز دو
        // فایل هم‌زمان به‌طور خاموش رسمی یک رویژن نمی‌شوند
        var isFirstOfficialForRevision = revisionId.HasValue &&
            !await _context.DocumentAttachments.AnyAsync(x =>
                x.DocumentRevisionId == revisionId.Value && x.IsActive && x.IsOfficial);

        var attachment =
            new DocumentAttachment
            {
                DocumentId = documentId,

                DocumentRevisionId = revisionId,

                FileRole = string.IsNullOrWhiteSpace(fileRole) ? null : fileRole,

                IsOfficial = isFirstOfficialForRevision,

                FileHash = fileHash,

                FileName = file.FileName,

                FileExtension =
                    Path.GetExtension(file.FileName),


                FilePath = storageKey,


                FileSize = file.Length,


                MimeType = file.ContentType,


                AIStatus = "Pending",


                UploadDate = DateTime.Now,


                IsActive = true
            };



        await _context.DocumentAttachments
            .AddAsync(attachment);



        await _context.SaveChangesAsync();


        var attachmentId = attachment.AttachmentId;

        // Hangfire: کار در دیتابیس ذخیره می‌شود (اگر برنامه Restart شود گم نمی‌شود) و در صورت خطا
        // خودکار Retry می‌شود - جایگزین Task.Run قبلی که بدون Persistence/Retry بود
        Hangfire.BackgroundJob.Enqueue<PEMS.Application.Interfaces.IDocumentAIIngestionService>(
            x => x.ProcessAttachmentAsync(attachmentId));


        return RedirectToAction(
            nameof(Details),
            new { id = documentId }
        );
    }





    // =========================
    // DOWNLOAD ATTACHMENT
    // =========================

    // طبق ممیزی امنیتی (P0 - Engineering File Security Containment): این Action قبلاً فقط
    // IsActive چک می‌کرد - هیچ محدودیت Scope/دیسیپلینی نداشت و مستقیم زیر wwwroot می‌خواند.
    // منطق واقعی (احراز هویت → مجوز/Scope → بررسی دسترسی والد → یافتن پیوست → Resolve
    // ذخیره‌سازی → Streaming امن) اکنون یک‌جا در FilesController است - این Route قدیمی
    // (که در Viewها/لینک‌های قبلی استفاده شده) حفظ می‌شود اما فقط به همان مسیر امن هدایت می‌کند،
    // نه یک پیاده‌سازی موازی و ضعیف‌تر.
    public IActionResult DownloadAttachment(int id)
    {
        return RedirectToAction("Download", "Files", new { id });
    }





    // =========================
    // DELETE ATTACHMENT
    // =========================



    // =========================
    // CREATE REVISION
    // =========================


}