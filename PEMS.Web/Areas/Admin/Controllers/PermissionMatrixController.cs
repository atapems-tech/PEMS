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

        var matrix = await _service.GetMatrixAsync();

        ViewBag.Roles = SystemRoles.All;
        ViewBag.Modules = new[]
        {
            "EngineeringDocuments", "VDR", "Contracts", "Vendors",
            "MaterialRequisition", "Equipment", "DisciplineWorkspace", "Correspondence",
            "WorkOrder", "StoreRequisition", "WorkRequest",
            "Task", "FunctionalLocation", "EquipmentCriticality", "JobPlan", "MaintenancePlan",
            "ProcurementPackage"
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
