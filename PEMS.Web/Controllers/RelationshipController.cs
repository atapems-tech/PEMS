using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// AI Relationship Engine UI: کاوش زنجیره‌ی روابط مهندسی از یک تجهیز، مدرک یا Vendor -
/// طبق مثال سند: Pump P-101 → Datasheet → Vendor → P&ID → ... → CMMS Asset.
/// </summary>
/// <remarks>
/// طبق «PEMS — P0 AI Pipeline Endpoint Hardening»: این Controller قبلاً هیچ بررسی مجوزی نداشت -
/// ConfirmSuggestedLink/BulkConfirm (که رابطه‌ی واقعی/FK بین Equipment و MR/VDR برقرار می‌کنند)
/// برای هر کاربر احرازهویت‌شده‌ای باز بودند. ماژول جدید "Relationship" چون هیچ ماژول موجودی این
/// قابلیت (تایید رابطه‌ی پیشنهادی بین چند نوع موجودیت مهندسی) را پوشش نمی‌دهد.
/// </remarks>
public class RelationshipController : Controller
{
    private readonly IRelationshipService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public RelationshipController(IRelationshipService service, ICurrentUserService currentUser,
        IPermissionService permissionService)
    {
        _service = service;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }




    // طبق درخواست: تایید دسته‌جمعی پیشنهادهای اتصال - چون تعداد تجهیزات زیاد است
    public async Task<IActionResult> BulkSuggestions(int? projectId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Relationship", "View"))
            return Forbid();

        ViewData["Title"] = "Bulk Approve Bindings";
        ViewData["Section"] = "AI Engineering Intelligence";

        var items = await _service.GetAllSuggestedLinksAsync(projectId);
        return View(items);
    }

    // طبق ممیزی بند ۱۲/۱۹: بررسی مجوز باید پیش از هر پردازش/نوشتنی رخ دهد - حتی برای Bulk.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkConfirm(List<string> selectedItems)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Relationship", "Approve"))
            return Forbid();

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
