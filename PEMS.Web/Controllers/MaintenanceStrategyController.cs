using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق تطبیق با RCM (SAE JA1011/JA1012): استراتژی نگهداری (Preventive/Predictive/
/// Corrective/CBM) پایه‌ی ساخت Maintenance Plan است. طبق درخواست: تعریف استراتژی برای چند
/// تجهیز هم‌زمان (نه فقط یکی‌یکی) و پیشنهاد نوع استراتژی بر اساس کلاس بحرانی‌بودن تجهیز.
///
/// طبق «PEMS Phase 7.5» ممیزی: این کنترلر پیش از این فاز هیچ بررسی مجوزی نداشت - هر کاربر
/// احرازهویت‌شده‌ای می‌توانست هر تعداد استراتژی برای هر تجهیزی بسازد/غیرفعال کند. اکنون
/// روی ماژول "MaintenanceStrategy" سخت‌سازی شده (Create=تکی، Approve=Bulk، طبق بند ۴۳).
/// </summary>
public class MaintenanceStrategyController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly IMaintenanceStrategyService _maintenanceStrategyService;
    private readonly IFunctionalLocationService _functionalLocationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public MaintenanceStrategyController(PEMSDbContext context, IMaintenanceStrategyService maintenanceStrategyService,
        IFunctionalLocationService functionalLocationService, ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _context = context;
        _maintenanceStrategyService = maintenanceStrategyService;
        _functionalLocationService = functionalLocationService;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    // طبق سند «PEMS — Global Data Grid Standard»: قبلاً کل جدول (یا زیرمجموعه‌ی فیلترشده با
    // equipmentId) با Include(Equipment)+ToListAsync (بدون Skip/Take) بارگذاری می‌شد.
    public async Task<IActionResult> Index(int? equipmentId, int page = 1, int? pageSize = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceStrategy", "View"))
            return Forbid();

        ViewData["Title"] = "Maintenance Strategies";
        ViewData["Section"] = "CMMS Integration";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var result = await _maintenanceStrategyService.GetListAsync(page, normalizedPageSize, equipmentId);

        ViewBag.SelectedEquipmentId = equipmentId;

        if (equipmentId.HasValue)
        {
            ViewBag.SelectedEquipmentLabel = await _context.Equipments
                .Where(x => x.EquipmentId == equipmentId.Value)
                .Select(x => x.TagNumber + " - " + x.Name)
                .FirstOrDefaultAsync();
        }

        return View(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int equipmentId, string strategyType, string? failureModeCode,
        string consequenceCategory, string? justification, string recommendedTriggerType,
        string? frequency, string? description, DateTime? startDate)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceStrategy", "Create"))
            return Forbid();

        // طبق «PEMS Phase 7.5» بند ۳۴: همان کلید تشخیص تکراری که RcmOperationalBridgeService
        // (فاز ۶.۱) از ابتدا برای MaintenanceStrategy تثبیت کرد - Equipment + FailureModeCode.
        var duplicate = await _context.MaintenanceStrategies.AnyAsync(x =>
            x.EquipmentId == equipmentId && x.IsActive && x.FailureModeCode == failureModeCode);
        if (duplicate)
        {
            TempData["Error"] = "یک استراتژی فعال با همین Failure Mode از قبل برای این تجهیز وجود دارد.";
            return RedirectToAction(nameof(Index), new { equipmentId });
        }

        await _context.MaintenanceStrategies.AddAsync(new MaintenanceStrategy
        {
            EquipmentId = equipmentId,
            StrategyType = strategyType,
            FailureModeCode = failureModeCode,
            ConsequenceCategory = string.IsNullOrWhiteSpace(consequenceCategory) ? "Operational" : consequenceCategory,
            Justification = justification,
            RecommendedTriggerType = string.IsNullOrWhiteSpace(recommendedTriggerType) ? "Calendar" : recommendedTriggerType,
            Frequency = frequency,
            Description = description,
            StartDate = startDate,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = "Maintenance strategy added";
        return RedirectToAction(nameof(Index), new { equipmentId });
    }

    // ==============================================================================
    // طبق درخواست: تعریف استراتژی برای چند تجهیز هم‌زمان - چون در عمل RCM معمولاً روی
    // خانواده‌ای از تجهیزات مشابه (مثلاً همه‌ی پمپ‌های سانتریفیوژ) اعمال می‌شه، نه یکی‌یکی.
    // ==============================================================================
    [HttpGet]
    public async Task<IActionResult> BulkCreate(List<int>? disciplineIds, List<string>? equipmentTypes,
        List<int>? functionalLocationIds, string? tagSearch)
    {
        // طبق بند ۴۳/۲۸: فعال‌سازی هم‌زمان چند تجهیز، بالاترین ریسک این کنترلر است - فقط
        // Supervisor/Manager (Approve)، نه MaintenanceEngineer.
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceStrategy", "Approve"))
            return Forbid();

        ViewData["Title"] = "Bulk Define Maintenance Strategy";
        ViewData["Section"] = "CMMS Integration";

        disciplineIds ??= new List<int>();
        equipmentTypes ??= new List<string>();
        functionalLocationIds ??= new List<int>();

        // ==============================================================================
        // طبق «PEMS Phase 7.6» بند ۲۴: ممیزی فاز ۷.۵ این ریسک را شناسایی کرد - این فرم برای
        // «فعال‌سازی کنترل‌شده» است، نه مرور کل ناوگان؛ بدون حداقل یک فیلتر، کل ۲۱٬۶۲۵ تجهیز
        // بدون صفحه‌بندی در یک فرم چندانتخابی رندر می‌شد. حالا بدون فیلتر، هیچ تجهیزی نشان داده
        // نمی‌شود؛ حتی با فیلتر، نتیجه به یک سقف صریح محدود است (نه صفحه‌بندی - چون این یک فرم
        // انتخاب‌چندگانه‌ی تک‌صفحه‌ای است، نه یک Grid). این سقف و این گیت هنوز همان محافظت
        // عمدی فاز ۷.۶ هستند - طبق ممیزی «Bulk Define Maintenance Strategy» تغییر نکردند.
        //
        // طبق همان ممیزی: Discipline/EquipmentType از تک‌انتخابی به چندانتخابی ارتقا یافتند
        // (Equipment خودش از ابتدا چندانتخابی بود - فقط ابعاد فیلتر تک‌انتخابی بودند)، یک فیلتر
        // Tag واقعی اضافه شد (TagNumber ۱۰۰٪ روی داده‌ی زنده پر است - قابل‌اتکا)، و یک فیلتر
        // Functional Location اضافه شد که از همان الگوی Picker موجود (IFunctionalLocationService،
        // دقیقاً همان چیزی که WorkRequest/Permit/Asset استفاده می‌کنند) بازاستفاده می‌کند - ولی
        // چون Equipment.FunctionalLocationId فعلاً ۰ از ۲۱٬۶۲۵ پر است، این فیلتر امروز عملاً
        // هیچ نتیجه‌ای محدود نمی‌کند؛ این یک محدودیت داده‌ی واقعی است، نه یک نقص کد - در View
        // صریح توضیح داده می‌شود.
        // ==============================================================================
        const int maxResults = 200;
        List<PEMS.Domain.Entities.Equipment> equipmentList;
        var hasFilter = disciplineIds.Count > 0 || equipmentTypes.Count > 0 ||
                         functionalLocationIds.Count > 0 || !string.IsNullOrWhiteSpace(tagSearch);

        if (!hasFilter)
        {
            equipmentList = new List<PEMS.Domain.Entities.Equipment>();
            ViewBag.NoFilterApplied = true;
        }
        else
        {
            // طبق ممیزی: Include(Discipline) این‌جا هرگز واقعاً استفاده نمی‌شد (View فقط از
            // ViewBag.Disciplines جدا برای Dropdown فیلتر استفاده می‌کند، نه از این ردیف‌ها) -
            // یک Include بلااستفاده حذف شد.
            var query = _context.Equipments
                .Where(x => x.IsActive)
                .AsQueryable();

            if (disciplineIds.Count > 0) query = query.Where(x => disciplineIds.Contains(x.DisciplineId));
            if (equipmentTypes.Count > 0) query = query.Where(x => equipmentTypes.Contains(x.EquipmentType));
            if (functionalLocationIds.Count > 0)
                query = query.Where(x => x.FunctionalLocationId.HasValue && functionalLocationIds.Contains(x.FunctionalLocationId.Value));
            if (!string.IsNullOrWhiteSpace(tagSearch))
                query = query.Where(x => x.TagNumber.Contains(tagSearch.Trim()));

            ViewBag.MatchingCount = await query.CountAsync();
            equipmentList = await query.OrderBy(x => x.TagNumber).Take(maxResults).ToListAsync();
            ViewBag.NoFilterApplied = false;
        }
        ViewBag.MaxResults = maxResults;

        // طبق RCM: کلاس بحرانی‌بودن هر تجهیز رو کنارش نشون می‌دیم تا انتخاب استراتژی
        // آگاهانه باشه (مثلاً کلاس A بهتره Preventive/Predictive بگیره، نه Corrective) - فقط
        // برای همین تجهیزات فیلترشده، نه کل جدول.
        var equipmentIds = equipmentList.Select(x => x.EquipmentId).ToList();
        var criticalityByEquipment = await _context.EquipmentCriticalities
            .Where(x => x.IsActive && equipmentIds.Contains(x.EquipmentId))
            .ToDictionaryAsync(x => x.EquipmentId, x => x.CriticalityClass);

        ViewBag.Equipments = equipmentList;
        ViewBag.CriticalityByEquipment = criticalityByEquipment;

        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();

        ViewBag.EquipmentTypes = await _context.Equipments.Where(x => x.IsActive && x.EquipmentType != "")
            .Select(x => x.EquipmentType).Distinct().OrderBy(x => x).ToListAsync();

        ViewBag.FunctionalLocations = await _functionalLocationService.GetPickerListAsync();

        // طبق بند ۸ ممیزی: آیا اصلاً داده‌ای برای فیلتر Functional Location وجود دارد - View
        // این را برای پیام صادقانه (نه پنهان‌کردن محدودیت) استفاده می‌کند.
        ViewBag.EquipmentWithFunctionalLocationCount = await _context.Equipments.CountAsync(x => x.IsActive && x.FunctionalLocationId != null);

        ViewBag.SelectedDisciplineIds = disciplineIds;
        ViewBag.SelectedEquipmentTypes = equipmentTypes;
        ViewBag.SelectedFunctionalLocationIds = functionalLocationIds;
        ViewBag.SelectedTagSearch = tagSearch;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkCreate(
        List<int> equipmentIds, string strategyType, string? failureModeCode,
        string consequenceCategory, string? justification, string recommendedTriggerType,
        string? frequency, string? description, DateTime? startDate)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceStrategy", "Approve"))
            return Forbid();

        if (equipmentIds == null || equipmentIds.Count == 0)
        {
            TempData["Error"] = "حداقل یک تجهیز باید انتخاب بشه.";
            return RedirectToAction(nameof(BulkCreate));
        }

        // طبق بند ۱۳ ممیزی «Bulk Define Maintenance Strategy»: فرم فقط بر اساس تجهیزات فعال
        // فیلتر می‌کند، ولی POST قبلاً هرگز دوباره تایید نمی‌کرد که شناسه‌های ارسالی هنوز فعال‌اند -
        // یک شکاف اعتبارسنجی واقعی سمت سرور (نه فقط اعتماد به فیلتر سمت کلاینت/GET قبلی).
        var activeEquipmentIds = await _context.Equipments
            .Where(x => equipmentIds.Contains(x.EquipmentId) && x.IsActive)
            .Select(x => x.EquipmentId)
            .ToListAsync();
        var activeEquipmentIdSet = activeEquipmentIds.ToHashSet();
        var inactiveOrMissingCount = equipmentIds.Distinct().Count() - activeEquipmentIdSet.Count;

        // طبق بند ۳۴: هیچ تجهیزی که از قبل استراتژی فعال با همین Failure Mode دارد، تکراری نمی‌شود
        var existing = await _context.MaintenanceStrategies
            .Where(x => x.IsActive && equipmentIds.Contains(x.EquipmentId) && x.FailureModeCode == failureModeCode)
            .Select(x => x.EquipmentId)
            .ToListAsync();
        var existingSet = existing.ToHashSet();

        var created = 0;
        foreach (var equipmentId in equipmentIds.Distinct())
        {
            if (!activeEquipmentIdSet.Contains(equipmentId)) continue;
            if (existingSet.Contains(equipmentId)) continue;

            await _context.MaintenanceStrategies.AddAsync(new MaintenanceStrategy
            {
                EquipmentId = equipmentId,
                StrategyType = strategyType,
                FailureModeCode = failureModeCode,
                ConsequenceCategory = string.IsNullOrWhiteSpace(consequenceCategory) ? "Operational" : consequenceCategory,
                Justification = justification,
                RecommendedTriggerType = string.IsNullOrWhiteSpace(recommendedTriggerType) ? "Calendar" : recommendedTriggerType,
                Frequency = frequency,
                Description = description,
                StartDate = startDate,
                IsActive = true,
                CreatedDate = DateTime.Now
            });
            created++;
        }

        await _context.SaveChangesAsync();

        var message = $"استراتژی برای {created} تجهیز ثبت شد";
        var notes = new List<string>();
        if (existingSet.Count > 0) notes.Add($"{existingSet.Count} مورد به‌دلیل داشتن استراتژی فعال مشابه، رد شد");
        if (inactiveOrMissingCount > 0) notes.Add($"{inactiveOrMissingCount} مورد غیرفعال/نامعتبر بود و رد شد");
        TempData["Message"] = message + (notes.Count > 0 ? $" ({string.Join("، ", notes)})." : ".");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int strategyId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceStrategy", "Edit"))
            return Forbid();

        var strategy = await _context.MaintenanceStrategies.FirstOrDefaultAsync(x => x.MaintenanceStrategyId == strategyId);
        if (strategy == null) return NotFound();

        strategy.IsActive = false;
        await _context.SaveChangesAsync();

        TempData["Message"] = "Strategy deactivated";
        return RedirectToAction(nameof(Index));
    }
}
