using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Controllers;

/// <summary>
/// AI Relationship Engine UI: کاوش زنجیره‌ی روابط مهندسی از یک تجهیز، مدرک یا Vendor -
/// طبق مثال سند: Pump P-101 → Datasheet → Vendor → P&ID → ... → CMMS Asset.
/// </summary>
public class RelationshipController : Controller
{
    private readonly IRelationshipService _service;

    public RelationshipController(IRelationshipService service)
    {
        _service = service;
    }

    [HttpGet]
    public IActionResult Index(string? q)
    {
        ViewData["Title"] = "Engineering Relationship Explorer";
        ViewData["Section"] = "AI Engineering Intelligence";

        ViewBag.Query = q;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Search(string q)
    {
        ViewData["Title"] = "Engineering Relationship Explorer";
        ViewData["Section"] = "AI Engineering Intelligence";

        ViewBag.Query = q;

        var result = string.IsNullOrWhiteSpace(q) ? null : await _service.ExploreByFreeTextAsync(q);

        return View("Index", result);
    }

    public async Task<IActionResult> Equipment(int id)
    {
        ViewData["Title"] = "Engineering Relationship Explorer";
        ViewData["Section"] = "AI Engineering Intelligence";

        var result = await _service.ExploreFromEquipmentAsync(id);
        if (result == null) return NotFound();

        return View("Index", result);
    }

    public async Task<IActionResult> Document(int id)
    {
        ViewData["Title"] = "Engineering Relationship Explorer";
        ViewData["Section"] = "AI Engineering Intelligence";

        var result = await _service.ExploreFromDocumentAsync(id);
        if (result == null) return NotFound();

        return View("Index", result);
    }

    public async Task<IActionResult> Vendor(int id)
    {
        ViewData["Title"] = "Engineering Relationship Explorer";
        ViewData["Section"] = "AI Engineering Intelligence";

        var result = await _service.ExploreFromVendorAsync(id);
        if (result == null) return NotFound();

        return View("Index", result);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmSuggestedLink(int equipmentId, string targetType, int targetId, bool force = false)
    {
        try
        {
            await _service.ConfirmSuggestedLinkAsync(equipmentId, targetType, targetId, force);
            TempData["Message"] = "اتصال تایید و برقرار شد.";
        }
        catch (Exception ex)
        {
            // طبق ممیزی بند ۱.۴: پیام خطا شامل راهنمایی برای تایید مجدد با force=true است
            TempData["Error"] = ex.Message;
            TempData["ConflictEquipmentId"] = equipmentId;
            TempData["ConflictTargetType"] = targetType;
            TempData["ConflictTargetId"] = targetId;
        }

        return RedirectToAction(nameof(Equipment), new { id = equipmentId });
    }

    // طبق ممیزی بند ۲.۱: گزارش تحلیل شکاف برای برنامه‌ریزی
    public async Task<IActionResult> GapReport(int? projectId)
    {
        ViewData["Title"] = "Relationship Gap Report";
        ViewData["Section"] = "AI Engineering Intelligence";

        var report = await _service.GetGapReportAsync(projectId);
        return View(report);
    }

    // طبق درخواست: تایید دسته‌جمعی پیشنهادهای اتصال - چون تعداد تجهیزات زیاد است
    public async Task<IActionResult> BulkSuggestions(int? projectId)
    {
        ViewData["Title"] = "تایید دسته‌جمعی اتصال‌ها";
        ViewData["Section"] = "AI Engineering Intelligence";

        var items = await _service.GetAllSuggestedLinksAsync(projectId);
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkConfirm(List<string> selectedItems)
    {
        if (selectedItems == null || selectedItems.Count == 0)
        {
            TempData["Error"] = "هیچ موردی انتخاب نشده.";
            return RedirectToAction(nameof(BulkSuggestions));
        }

        // هر مورد به‌صورت "equipmentId|targetType|targetId" فرستاده می‌شه (طبق فرمت چک‌باکس‌های View)
        var parsed = new List<(int EquipmentId, string TargetType, int TargetId)>();
        foreach (var raw in selectedItems)
        {
            var parts = raw.Split('|');
            if (parts.Length == 3 && int.TryParse(parts[0], out var eqId) && int.TryParse(parts[2], out var targetId))
                parsed.Add((eqId, parts[1], targetId));
        }

        var (confirmed, errors) = await _service.BulkConfirmAsync(parsed);

        TempData["Message"] = $"{confirmed} مورد تایید و برقرار شد.";
        if (errors.Any())
            TempData["Error"] = $"{errors.Count} مورد خطا داشت: " + string.Join(" | ", errors.Take(5));

        return RedirectToAction(nameof(BulkSuggestions));
    }
}
