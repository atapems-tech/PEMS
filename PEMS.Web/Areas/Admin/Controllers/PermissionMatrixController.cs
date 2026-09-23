using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Identity;

namespace PEMS.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class PermissionMatrixController : Controller
{
    private readonly IPermissionService _service;

    public PermissionMatrixController(IPermissionService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Permission Matrix";
        ViewData["Section"] = "Administration";
        ViewData["HelpKey"] = "Admin.PermissionMatrix.Index";

        var matrix = await _service.GetMatrixAsync();

        ViewBag.Roles = SystemRoles.All;
        // طبق «PEMS — Global Engineering Management UI/UX, Navigation & RBAC Hardening» بند ۲۰:
        // Inspection/Transmittal قبلاً از این آرایه غایب بودند با اینکه هر دو واقعاً از طریق
        // HasPermissionAsync در InspectionController/TransmittalController گیت می‌شوند و در
        // SeedDefaultsAsync هم Seed دارند - یعنی SystemAdmin عملاً نمی‌توانست این دو ماژول را
        // از طریق همین صفحه ببیند یا ویرایش کند. MDRImport/EquipmentImport/MrsImport هم تازه
        // اضافه شدند (طبق بند ۹-۱۲). AIExtraction/TableExtraction/Relationship/OCRProcessing هم
        // تازه اضافه شدند (طبق «PEMS — P0 AI Pipeline Endpoint Hardening»). Team/Crew/
        // InventoryMasterData/MaintenanceMasterData هم تازه اضافه شدند (طبق «PEMS — P1
        // Organization & Master Data Authorization Hardening»).
        ViewBag.Modules = new[]
        {
            "EngineeringDocuments", "VDR", "Contracts", "Vendors",
            "MaterialRequisition", "Equipment", "DisciplineWorkspace", "Correspondence",
            "WorkOrder", "StoreRequisition", "WorkRequest", "Inspection", "Transmittal",
            "Task", "FunctionalLocation", "EquipmentCriticality", "JobPlan", "MaintenancePlan",
            "ProcurementPackage",
            "MDRImport", "EquipmentImport", "MrsImport",
            "AIExtraction", "TableExtraction", "Relationship", "OCRProcessing",
            "Team", "Crew", "InventoryMasterData", "MaintenanceMasterData",
            "Setup",
            // طبق ممیزی Material Management (Gap A) + Step 2.
            "IRN",
            // طبق ممیزی Material Management Step 1/3 (یافته امنیتی).
            "MaterialItem",
            // طبق ممیزی Package Procurement Contract Finalization (§C).
            "Tender",
            // طبق ممیزی Package Procurement Contract Finalization (§H، Phase 7.5).
            "PurchaseOrder"
        };

        return View(matrix);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(string roleName, List<ModulePermissionInput> permissions)
    {
        await _service.SetPermissionsAsync(roleName, permissions ?? new List<ModulePermissionInput>());
        TempData["Message"] = $"Updated permissions for {roleName}";
        return RedirectToAction(nameof(Index));
    }
}
