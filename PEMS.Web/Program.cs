using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using PEMS.Application.Interfaces;
using PEMS.Application.Services;

using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Repositories;
using PEMS.Persistence.Services;

using PEMS.Application.AI.Interfaces;
using PEMS.Application.AI.OCR;
using PEMS.Application.AI.Parser;
using PEMS.Application.AI.Search.Interfaces;
using PEMS.Application.AI.Search.Services;
using PEMS.Application.AI.Services;

using PEMS.AI.Interfaces;
using PEMS.AI.Services;

using PEMS.Web.Security;
using PEMS.Infrastructure.Cache;
using PEMS.WorkflowPlatform.Interfaces;
using PEMS.WorkflowPlatform.Services;

using Hangfire;
using Hangfire.Dashboard;
using Serilog;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;


// Structured Logging (طبق تکنولوژی‌های تعیین‌شده در Bible - Volume II): جایگزین Logging پیش‌فرض
// برای عیب‌یابی بهتر در Production - خروجی هم روی Console هم فایل روزانه
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/pems-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();


var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();



// =========================
// MVC
// =========================

// =========================
// Bilingual Platform (English / Persian با RTL خودکار)
// =========================
// طبق ممیزی: چون SharedResource.cs خودش داخل پوشه‌ی Resources قرار دارد، مقداردهی
// ResourcesPath="Resources" باعث می‌شد نام کامل منبع مورد انتظار دوبار "Resources" را
// تکرار کند (PEMS.Web.Resources.Resources.SharedResource) که هرگز با نام واقعی resx
// جفت نمی‌شد - یعنی IStringLocalizer همیشه به‌جای ترجمه، خودِ کلید را برمی‌گرداند، چه
// برای انگلیسی چه فارسی. چون namespace کلاس از قبل شامل "Resources" است، اینجا باید خالی
// بماند.
builder.Services.AddLocalization(options => options.ResourcesPath = "");

// طبق درخواست: اعلان‌های لحظه‌ای بدون Refresh
builder.Services.AddSignalR();

builder.Services.AddScoped<PEMS.Application.Interfaces.INotificationBroadcaster, PEMS.Web.Hubs.SignalRNotificationBroadcaster>();

// یک [Authorize] سراسری روی همه‌ی صفحات؛ فقط اکشن‌هایی که [AllowAnonymous] دارند
// (مثل صفحه‌ی Login) بدون ورود قابل دسترسی هستند.
builder.Services.AddControllersWithViews(options =>
{
    var policy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.Filters.Add(
        new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter(policy));

    // طبق CMMS Production Baseline v1 بند P0-03: بعد از AuthorizeFilter اجرا می‌شود - اول
    // احراز هویت، بعد بررسی MustChangePassword روی همه‌ی درخواست‌ها (نه فقط لحظه‌ی Login).
    options.Filters.Add(new PEMS.Web.Security.PasswordChangeGateFilter());
})
.AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
.AddDataAnnotationsLocalization();




// =========================
// Database
// =========================

builder.Services.AddDbContext<PEMSDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("PEMSConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure();
        }
    ));


// =========================
// Background Jobs (Hangfire)
// =========================
// جایگزین Task.Run بدون Persistence/Retry که برای پردازش AI استفاده می‌شد: حالا صف کارها روی
// همان دیتابیس SQL Server ذخیره می‌شود (اگر برنامه Restart شود، کار گم نمی‌شود)، Retry خودکار
// دارد، و یک Dashboard مدیریتی (/hangfire، فقط SystemAdmin) برای رصد وضعیت پردازش‌های AI دارد.

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("PEMSConnection")));

builder.Services.AddHangfireServer();


// =========================
// Caching (طبق Volume II-Bible: "Caching: Redis")
// =========================
// اگر Connection String واقعی Redis تنظیم شده باشد از Redis (توزیع‌شده، مناسب چندسروری)
// استفاده می‌شود؛ در غیر این صورت (پیش‌فرض) از IMemoryCache داخل همان Process - بدون نیاز به
// نصب/راه‌اندازی هیچ زیرساخت بیرونی. تغییر این تصمیم فقط همین‌جا انجام می‌شود، هیچ سرویس دیگری
// وابسته به این انتخاب نیست چون همه از طریق ICacheService کار می‌کنند.

var redisConnection = builder.Configuration.GetConnectionString("Redis");

builder.Services.AddMemoryCache();

if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
    });

    builder.Services.AddScoped<ICacheService, RedisCacheService>();
}
else
{
    builder.Services.AddScoped<ICacheService, MemoryCacheService>();
}




// =========================
// ASP.NET Core Identity (Authentication / Authorization)
// =========================

var authLockoutSection = builder.Configuration.GetSection("Authentication:Lockout");
var maxFailedAccessAttempts = authLockoutSection.GetValue<int?>("MaxFailedAccessAttempts") ?? 5;
var defaultLockoutMinutes = authLockoutSection.GetValue<int?>("DefaultLockoutMinutes") ?? 10;
var cookieSecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        // سیاست رمز عبور - برای محیط سازمانی داخلی؛ در صورت نیاز سخت‌گیرانه‌تر شود
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        options.User.RequireUniqueEmail = true;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = maxFailedAccessAttempts;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(defaultLockoutMinutes);
    })
    .AddEntityFrameworkStores<PEMSDbContext>()
    .AddDefaultTokenProviders();


builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    options.Cookie.Name = "PEMS.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
});


// =========================
// طبق سند PEMS-SEC-PERMIT-AUTH-001: WebAuthn/Passkey Relying Party برای Step-up Authentication.
// ServerDomain/Origins از appsettings خوانده می‌شود - مقدار پیش‌فرض برای محیط توسعه‌ی محلی است؛
// در Production باید دامنه‌ی واقعی HTTPS تنظیم شود (WebAuthn بدون HTTPS/localhost کار نمی‌کند).
// =========================
builder.Services.AddFido2(options =>
{
    options.ServerDomain = builder.Configuration["Fido2:ServerDomain"] ?? "localhost";
    options.ServerName = builder.Configuration["Fido2:ServerName"] ?? "PEMS";
    options.Origins = (builder.Configuration.GetSection("Fido2:Origins").Get<string[]>() ?? new[] { "http://localhost:5089" }).ToHashSet();
    options.TimestampDriftTolerance = 300000;
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddScoped<
    Microsoft.AspNetCore.Identity.IUserClaimsPrincipalFactory<ApplicationUser>,
    ApplicationUserClaimsPrincipalFactory>();




// =========================
// Application Services
// =========================

builder.Services.AddScoped<
    IEngineeringDocumentService,
    EngineeringDocumentService>();


builder.Services.AddScoped<
    IDocumentWorkflowService,
    DocumentWorkflowService>();


// ==============================================================================
// Workflow Platform (فاز A) - موتور عمومی گردش‌کار، طبق سند Baseline مستقل از هر ماژول.
// IWorkflowDbContext به همان PEMSDbContext در حال استفاده در این Request نگاشت می‌شود
// (نه یک DbContext/اتصال جدا) تا Instance/History همیشه در همان تراکنش رکورد کسب‌وکاری
// مربوطه ذخیره شوند.
// ==============================================================================
builder.Services.AddScoped<IWorkflowDbContext>(sp => sp.GetRequiredService<PEMSDbContext>());
builder.Services.AddScoped<IWorkflowRuntime, WorkflowRuntimeService>();

builder.Services.AddScoped<IVdrWorkflowService, VdrWorkflowService>();
builder.Services.AddScoped<ITechnicalQueryService, TechnicalQueryService>();
builder.Services.AddScoped<IMOCService, MOCService>();
builder.Services.AddScoped<IEngineeringCopilotService, EngineeringCopilotService>();
builder.Services.AddScoped<IWorkOrderReviewService, WorkOrderReviewService>();

// طبق سند معماری Employee/Team/Labor Cost - این سه سرویس ساخته شده بودن ولی جایی Register
// نشده بودن (باگ واقعی که موقع اولین درخواست به TeamController/WorkOrder Labor کرش می‌کرد)
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IWorkOrderLaborService, WorkOrderLaborService>();

builder.Services.AddScoped<IWorkOrderLifecycleService, WorkOrderLifecycleService>();
builder.Services.AddScoped<IStoreRequisitionLifecycleService, StoreRequisitionLifecycleService>();
builder.Services.AddScoped<IReliabilityService, ReliabilityService>();
builder.Services.AddScoped<IInspectionService, InspectionService>();
builder.Services.AddScoped<IKpiService, KpiService>();
builder.Services.AddScoped<IPermitService, PermitService>();
builder.Services.AddScoped<IAccessControlService, AccessControlService>();
builder.Services.AddScoped<IRoleModuleActionService, RoleModuleActionService>();
builder.Services.AddScoped<IIsolationService, IsolationService>();
builder.Services.AddScoped<IGasTestService, GasTestService>();
builder.Services.AddScoped<ISimopsService, SimopsService>();
builder.Services.AddScoped<IShiftHandoverService, ShiftHandoverService>();
builder.Services.AddScoped<IExecutionGateService, ExecutionGateService>();
builder.Services.AddScoped<IWebAuthnService, WebAuthnService>();
builder.Services.AddScoped<IPermitAuthorityService, PermitAuthorityService>();

builder.Services.AddScoped<IEmployeeSkillService, EmployeeSkillService>();

builder.Services.AddScoped<ICrewService, CrewService>();
builder.Services.AddScoped<IEmployeeLaborRateService, EmployeeLaborRateService>();

builder.Services.AddScoped<IMaterialItemService, MaterialItemService>();
builder.Services.AddScoped<IMaterialStockService, MaterialStockService>();
builder.Services.AddScoped<IInventoryTransactionService, InventoryTransactionService>();
builder.Services.AddScoped<IMrsImportService, MrsImportService>();

// ==============================================================================
// Global Intelligent Search Framework (فاز A) - طبق معماری: هر Provider جدید فقط با یک
// خط AddScoped اضافه ثبت می‌شود و خودکار توسط GlobalSearchService شناسایی می‌شود؛ خودِ
// GlobalSearchService هیچ تغییری برای موجودیت جدید نیاز ندارد.
// ==============================================================================
builder.Services.AddScoped<IGlobalSearchService, GlobalSearchService>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.EquipmentSearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.EngineeringDocumentSearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.VdrItemSearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.LetterSearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.VendorSearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.ProjectSearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.EmployeeSearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.DepartmentSearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.DisciplineSearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.MaterialRequisitionSearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.WorkOrderSearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.ContractSearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.DocumentRevisionSearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.TechnicalQuerySearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.MOCSearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.SparePartSearchProvider>();

// طبق فاز F: Abstraction کاربر جاری برای Providerهایی که نیاز به فیلتر بر اساس کاربر
// دارند (مثل Notification) - بدون وابسته‌کردن PEMS.Persistence به PEMS.Web
builder.Services.AddScoped<PEMS.Application.Interfaces.ICurrentUserContext, PEMS.Web.Security.CurrentUserContext>();

builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.EquipmentTagSearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.AttachmentSearchProvider>();
builder.Services.AddScoped<ISearchableEntityProvider, PEMS.Persistence.Services.SearchProviders.NotificationSearchProvider>();


builder.Services.AddScoped<IProjectService, ProjectService>();

builder.Services.AddScoped<ITransmittalService, TransmittalService>();

builder.Services.AddScoped<IMaterialRequisitionService, MaterialRequisitionService>();

builder.Services.AddScoped<IExcelExportService, ExcelExportService>();

builder.Services.AddScoped<IChatAssistantService, ChatAssistantService>();


builder.Services.AddScoped<IVendorService, VendorService>();

builder.Services.AddScoped<IContractService, ContractService>();

builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();

builder.Services.AddScoped<IVDRService, VDRService>();

builder.Services.AddScoped<IVDRImportService, VDRImportService>();

builder.Services.AddScoped<IMDRImportService, MDRImportService>();

builder.Services.AddScoped<IRoutingRuleService, RoutingRuleService>();

builder.Services.AddScoped<IIntakeService, IntakeService>();

builder.Services.AddScoped<ITableExtractionImportService, TableExtractionImportService>();

builder.Services.AddScoped<IRelationshipService, RelationshipService>();

builder.Services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();

builder.Services.AddScoped<ISavedFilterService, SavedFilterService>();

builder.Services.AddScoped<IDocumentNumberingService, DocumentNumberingService>();

// طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» بخش ۳: شماره‌گذاری خودکار ترانسمیتال (TV-X-UU-AAAA-NNNN)
builder.Services.AddScoped<ITransmittalNumberingService, TransmittalNumberingService>();

// طبق بخش ۴.۳.۱ همان سند: شماره‌گذاری رسمی مدرک فروشنده به فرمت CONTRACTOR
builder.Services.AddScoped<IVdrDocumentNumberingService, VdrDocumentNumberingService>();

// طبق بخش ۵.۲ همان سند: آپلود واقعی مدارک فروشنده روی FTP Server (بدون بخش ایمیل - طبق تصمیم کاربر، اپلیکیشن آفلاین است)
builder.Services.AddScoped<IFtpUploadService, FtpUploadService>();

// طبق بخش ۶-۷ همان سند: تحویل مستندات نهایی (MDB)
builder.Services.AddScoped<IManufacturerDataBookService, ManufacturerDataBookService>();

// طبق بخش M ممیزی سخت‌سازی مهندسی: چرخه‌ی Hold با تاریخچه‌ی کامل
builder.Services.AddScoped<IDocumentHoldService, DocumentHoldService>();

// طبق CMMS Production Baseline v1 بند P0-04: سرویس شماره‌گذاری مرکزی و اتمیک -
// جایگزین الگوهای Count()+1 پراکنده در WorkOrder/MaintenancePlan/Instrument/StoreRequisition.
builder.Services.AddScoped<INumberingService, NumberingService>();

// طبق CMMS Production Baseline v1 بند ۶ (CMMS-01: Asset Foundation)
builder.Services.AddScoped<IFunctionalLocationService, FunctionalLocationService>();

// طبق CMMS Production Baseline v1 بند ۱۰.۳ (CMMS-02: Maintenance Planning)
builder.Services.AddScoped<IJobPlanService, JobPlanService>();

// طبق CMMS Production Baseline v1 بند P0-05 (CMMS-03: Work Management)
builder.Services.AddScoped<IWorkRequestService, WorkRequestService>();

builder.Services.AddScoped<IUserDashboardService, UserDashboardService>();

builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddScoped<ILetterService, LetterService>();

builder.Services.AddScoped<ILetterReferralService, LetterReferralService>();

builder.Services.AddScoped<ITagService, TagService>();

builder.Services.AddScoped<ITaskService, TaskService>();

builder.Services.AddScoped<ICorrespondenceReportService, CorrespondenceReportService>();

builder.Services.AddScoped<IEngineeringReportService, EngineeringReportService>();

builder.Services.AddScoped<IVDRReportService, VDRReportService>();

builder.Services.AddScoped<ICmmsReportService, CmmsReportService>();

builder.Services.AddScoped<IProcurementReportService, ProcurementReportService>();

builder.Services.AddScoped<IMeetingService, MeetingService>();

builder.Services.AddScoped<IRichTextSanitizerService, RichTextSanitizerService>();

builder.Services.AddScoped<ILetterNumberingService, LetterNumberingService>();

builder.Services.AddScoped<IProcurementPackageService, ProcurementPackageService>();

builder.Services.AddScoped<IVendorRiskService, VendorRiskService>();

builder.Services.AddScoped<IDocumentCommentService, DocumentCommentService>();

builder.Services.AddScoped<IEquipmentImportService, EquipmentImportService>();

builder.Services.AddScoped<IEquipmentPdfScanService, EquipmentPdfScanService>();

builder.Services.AddScoped<IEquipmentService, EquipmentService>();

builder.Services.AddScoped<IAssetService, AssetService>();

builder.Services.AddScoped<IInstrumentService, InstrumentService>();

builder.Services.AddScoped<ISparePartService, SparePartService>();

builder.Services.AddScoped<IMaintenancePlanService, MaintenancePlanService>();

builder.Services.AddScoped<IMaintenanceStrategyService, MaintenanceStrategyService>();

builder.Services.AddScoped<IDisciplineWorkspaceService, DisciplineWorkspaceService>();

// طبق ممیزی امنیتی (P0 - Engineering File Security Containment): ریشه‌ی ذخیره‌سازی عمداً بیرون
// از wwwroot (خواهر آن، نه زیرشاخه‌اش) - این همان چیزی است که آسیب‌پذیری اصلی را می‌بندد؛
// UseStaticFiles فقط داخل wwwroot سرویس می‌دهد.
builder.Services.AddSingleton<PEMS.Application.Interfaces.IFileStorageService>(
    _ => new PEMS.Infrastructure.FileStorage.LocalFileStorageProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "filestore")));

builder.Services.AddScoped<IPermissionService, PermissionService>();

builder.Services.AddScoped<IEngineeringDocumentIntelligenceService, EngineeringDocumentIntelligenceService>();




// =========================
// Persistence Repositories
// =========================

builder.Services.AddScoped<
    IEngineeringDocumentRepository,
    EngineeringDocumentRepository>();




// =========================
// AI Services
// =========================

builder.Services.Configure<OcrOptions>(builder.Configuration.GetSection("OCR"));

builder.Services.AddScoped<IOcrService, TesseractOcrService>();

builder.Services.AddScoped<ITextExtractor, PdfTextExtractor>();
builder.Services.AddScoped<ITextExtractor, WordTextExtractor>();
builder.Services.AddScoped<ITextExtractor, ExcelTextExtractor>();
builder.Services.AddScoped<ITextExtractor, ImageTextExtractor>();

builder.Services.AddScoped<IDocumentTextExtractorFactory, DocumentTextExtractorFactory>();

builder.Services.AddScoped<IDocumentAIService, DocumentAIService>();

builder.Services.AddScoped<IDocumentAIIngestionService, DocumentAIIngestionService>();

builder.Services.AddScoped<IDocumentSearchAIService, DocumentSearchAIService>();


var ollamaUrl = builder.Configuration["Ollama:Url"] ?? "http://localhost:11434/";
if (!ollamaUrl.EndsWith('/')) ollamaUrl += "/";

var ollamaTimeoutMinutes = builder.Configuration.GetValue<double?>("Ollama:TimeoutMinutes") ?? 15;

builder.Services.AddHttpClient<IOllamaService, OllamaService>(client =>
{
    client.BaseAddress = new Uri(ollamaUrl);
    client.Timeout = TimeSpan.FromMinutes(ollamaTimeoutMinutes);
});




var app = builder.Build();




// =========================
// Seed Roles / Default Admin User
// =========================

using (var scope = app.Services.CreateScope())
{
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);

    // دپارتمان‌های پیش‌فرض
    await PEMS.Persistence.Seed.DepartmentSeeder.SeedAsync(scope.ServiceProvider);

    // دیسیپلین‌های مهندسی پیش‌فرض - بدون این Seed، پس از پاک‌سازی دیتابیس Workspace دیسیپلین‌ها
    // خالی می‌شوند و UI مربوط به default items/discipline list هیچ آیتمی نشان نمی‌دهد.
    await PEMS.Persistence.Seed.DisciplineSeeder.SeedAsync(scope.ServiceProvider);

    var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
    await permissionService.SeedDefaultsAsync();

    // طبق CMMS-10a ADR-02: پایه‌ی مجوزدهی عمومی Role/Module/Action - فقط برای Permit
    var roleModuleActionService = scope.ServiceProvider.GetRequiredService<IRoleModuleActionService>();
    await roleModuleActionService.SeedDefaultsAsync();

    // طبق سند RBAC-ABAC Authorization Architecture (فاز ۱): داده‌ی RolePermission/
    // RoleModuleAction/AspNetUserRoles بالا را به مدل عمومی جدید (Permission/RolePermissionGrant/
    // RoleAssignment) کپی می‌کند - باید بعد از دو Seed بالا اجرا شود
    await PEMS.Persistence.Seed.AccessControlSeeder.SeedFromLegacyAsync(scope.ServiceProvider);

    // طبق سند RBAC-ABAC Authorization Architecture (فاز ۳): دو مجوز جدید معادل آرایه‌های
    // Hard-Code شده‌ی UnrestrictedRoles/ClientResponseRoles که این فاز جایگزین می‌کند
    await PEMS.Persistence.Seed.AccessControlSeeder.SeedPhase3PermissionsAsync(scope.ServiceProvider);

    // طبق فاز ۵ (Governance): سیاست‌های SoD پیش‌فرض برای چند مسیر حساس ساخته می‌شوند تا
    // خودتاییدی و Close/Approve در همان رکورد تا حداقل از سطح داده‌ای جلوگیری شود.
    await PEMS.Persistence.Seed.AccessControlSeeder.SeedPhase5GovernanceAsync(scope.ServiceProvider);

    var routingRuleService = scope.ServiceProvider.GetRequiredService<IRoutingRuleService>();
    await routingRuleService.SeedDefaultsAsync();

    // فاز C - Workflow Platform: تعریف گردش‌کار «تایید مدرک مهندسی» را Seed می‌کند
    // (Idempotent) و مدارک از‌قبل‌درجریان را که هنوز Instance ندارند منتقل می‌کند.
    await PEMS.Persistence.Seed.EngineeringDocumentWorkflowSeeder.SeedAsync(scope.ServiceProvider);
    await PEMS.Persistence.Seed.EngineeringDocumentWorkflowBackfillService.BackfillAsync(scope.ServiceProvider);

    // گردش‌کار بازبینی VDR (طبق استاندارد جهانی مکاتبات وندور)
    await PEMS.Persistence.Seed.VdrWorkflowSeeder.SeedAsync(scope.ServiceProvider);

    // فاز ۱ نقشه‌راه معماری: TQ/TSR/RFI مستقل
    await PEMS.Persistence.Seed.TechnicalQueryWorkflowSeeder.SeedAsync(scope.ServiceProvider);

    // فاز ۲ نقشه‌راه معماری: MOC مستقل
    await PEMS.Persistence.Seed.MOCWorkflowSeeder.SeedAsync(scope.ServiceProvider);

    // بازنگری CMMS: نقش‌های پیش‌فرض تعمیرونگهداری
    await PEMS.Persistence.Seed.MaintenanceRoleSeeder.SeedAsync(scope.ServiceProvider);
    await PEMS.Persistence.Seed.MaintenanceGroupSeeder.SeedAsync(scope.ServiceProvider);
    await PEMS.Persistence.Seed.SkillSeeder.SeedAsync(scope.ServiceProvider);
    await PEMS.Persistence.Seed.QualificationSeeder.SeedAsync(scope.ServiceProvider);
    await PEMS.Persistence.Seed.CertificationSeeder.SeedAsync(scope.ServiceProvider);

    // طبق درخواست: گردش‌کار بازبینی چند-دیسیپلینه WorkOrder
    await PEMS.Persistence.Seed.WorkOrderReviewWorkflowSeeder.SeedAsync(scope.ServiceProvider);

    // بخش QC/QA - دیسیپلین‌های استاندارد بازرسی
    await PEMS.Persistence.Seed.QCDisciplineSeeder.SeedAsync(scope.ServiceProvider);
    await PEMS.Persistence.Seed.NcrApprovalWorkflowSeeder.SeedAsync(scope.ServiceProvider);

    // طبق CMMS-10b: زنجیره‌ی تایید Permit to Work
    await PEMS.Persistence.Seed.PermitApprovalWorkflowSeeder.SeedAsync(scope.ServiceProvider);

    // طبق CMMS-10d: حد پذیرش پارامترهای گاز + قاعده‌ی مدت اعتبار تست گاز
    await PEMS.Persistence.Seed.GasTestSeeder.SeedAsync(scope.ServiceProvider);

    // طبق CMMS-10e: قاعده‌ی شدت تعارض SIMOPS بر اساس جفت PermitType
    await PEMS.Persistence.Seed.SimopsSeeder.SeedAsync(scope.ServiceProvider);

    // طبق CMMS-10f: کنترل‌های الزامی اجرا بر اساس نوع Permit
    await PEMS.Persistence.Seed.ExecutionGateSeeder.SeedAsync(scope.ServiceProvider);

    // طبق سند Inventory & Material Management: انبارها از پیشوندهای واقعی MRS، دسته‌بندی و واحد پایه
    await PEMS.Persistence.Seed.WarehouseSeeder.SeedAsync(scope.ServiceProvider);
    await PEMS.Persistence.Seed.MaterialCategorySeeder.SeedAsync(scope.ServiceProvider);
    await PEMS.Persistence.Seed.MaterialUOMSeeder.SeedAsync(scope.ServiceProvider);

    // طبق CMMS Production Baseline v1 بند P0-04: شمارنده‌ی WorkOrder را از روی شماره‌های قدیمی
    // (پیش از NumberingService) Seed می‌کند تا شماره‌ی بعدی با آن‌ها برخورد نکند.
    await PEMS.Persistence.Seed.NumberingBackfillSeeder.SeedAsync(scope.ServiceProvider);
}




// =========================
// HTTP Pipeline
// =========================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}



app.UseHttpsRedirection();

// طبق ممیزی امنیتی (P0 - Engineering File Security Containment): بند صریح "Direct /uploads URL
// → unavailable" - این یک قفل دفاع-در-عمق است، مستقل از این‌که آیا همه‌ی فایل‌های فیزیکی از
// wwwroot/uploads به ریشه‌ی امن جدید منتقل شده‌اند یا نه. حتی اگر یک فایل به اشتباه در آینده
// دوباره زیر یکی از این پوشه‌ها نوشته شود، این Middleware (قبل از UseStaticFiles) هر درخواست را
// با ۴۰۴ رد می‌کند - UseStaticFiles اصلاً به آن نمی‌رسد.
//
// عمداً فقط پوشه‌های سند کسب‌وکاری/مهندسی (که به IFileStorageService مهاجرت کردند) مسدود
// می‌شوند - نه کل /uploads: wwwroot/uploads/profile-photos و /signatures متعلق به MyProfile
// هستند (عکس/امضای شخصی، نه سند مهندسی محرمانه) و عمداً خارج از محدوده‌ی این وظیفه‌ی امنیتی
// مشخص باقی مانده‌اند (طبق گزارش نهایی) - مسدودکردن کورکورانه‌ی کل /uploads قابلیت نمایش
// عکس/امضای پروفایل را که امروز کار می‌کند می‌شکند.
var blockedUploadFolders = new[]
{
    "/uploads/documents", "/uploads/engineering-comments", "/uploads/vdr", "/uploads/moc",
    "/uploads/technical-queries", "/uploads/letters", "/uploads/mrs-import", "/uploads/intake"
};
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (blockedUploadFolders.Any(f => path.StartsWithSegments(f, StringComparison.OrdinalIgnoreCase)))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});

app.UseStaticFiles();


// Bilingual Platform: زبان از یک Cookie خوانده می‌شود (پیش‌فرض انگلیسی)؛ سوییچر زبان در
// _Layout.cshtml یک کوکی می‌سازد و صفحه را Reload می‌کند.
//
// مهم: تقویم پیش‌فرض CultureInfo("fa") در دات‌نت، تقویم شمسی (PersianCalendar) است - نه
// میلادی. یعنی بدون این override، به محض سوییچ به فارسی، هر DateTime.ToString()/Parse()ای
// که صریحاً CultureInfo.InvariantCulture را مشخص نکرده باشد (که در سراسر پروژه معمول است،
// مثل Timestamp های AuditLog یا نام فایل‌های Export) خاموش به تقویم شمسی تبدیل می‌شود، چون
// همه‌ی تاریخ‌های دیتابیس میلادی ذخیره شده‌اند. فارسی فقط باید متن/جهت RTL را عوض کند، تقویم
// را نه؛ پس تقویم روی همان CultureInfo("fa") به‌صراحت میلادی نگه داشته می‌شود.
var faCulture = new System.Globalization.CultureInfo("fa");
faCulture.DateTimeFormat.Calendar = new System.Globalization.GregorianCalendar();
var enCulture = new System.Globalization.CultureInfo("en");
var supportedCultureInfos = new List<System.Globalization.CultureInfo> { enCulture, faCulture };

var localizationOptions = new Microsoft.AspNetCore.Builder.RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(enCulture, enCulture),
    SupportedCultures = supportedCultureInfos,
    SupportedUICultures = supportedCultureInfos
};

localizationOptions.RequestCultureProviders.Insert(0,
    new Microsoft.AspNetCore.Localization.CookieRequestCultureProvider());

app.UseRequestLocalization(localizationOptions);


app.UseRouting();


// ترتیب مهم است: احراز هویت باید قبل از احراز دسترسی اجرا شود
app.UseAuthentication();

app.UseAuthorization();


// Audit Log: قبل از رسیدن به هر Controller، اطلاعات کاربر جاری را روی PEMSDbContext (که Scoped است)
// می‌گذاریم تا SaveChanges/SaveChangesAsync بتواند آن‌ها را در AuditLog ثبت کند.
app.Use(async (context, next) =>
{
    var dbContext = context.RequestServices.GetRequiredService<PEMS.Persistence.Context.PEMSDbContext>();
    var currentUser = context.RequestServices.GetRequiredService<ICurrentUserService>();

    dbContext.CurrentUserId = currentUser.UserId;
    dbContext.CurrentUserName = currentUser.UserName;

    await next();
});



app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthFilter() }
});


app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
);


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.MapHub<PEMS.Web.Hubs.NotificationHub>("/hubs/notifications");



app.Run();

// طبق ممیزی امنیتی (P0 - تست‌های امنیتی WebApplicationFactory): کلاس Program تولیدشده از
// Top-Level Statements پیش‌فرض internal است - این Partial Class آن را از پروژه‌ی تست هم قابل‌ارجاع
// می‌کند، بدون تغییر هیچ رفتار اجرایی برنامه
public partial class Program { }
