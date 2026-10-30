using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// SCRUM-470 (owner decision 2026-10-29): the PO authority table, separate from the contract rules. Nothing is seeded; the
/// SystemAdmin defines every level (amount range, currency, approver role). Same access as the contract authority page.
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class PurchaseOrderAuthorityController : Controller
{
    private readonly IPurchaseOrderApprovalService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly PEMSDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public PurchaseOrderAuthorityController(IPurchaseOrderApprovalService service, ICurrentUserService currentUser, PEMSDbContext context,
        IStringLocalizer<SharedResource> localizer)
    {
        _service = service;
        _currentUser = currentUser;
        _context = context;
        _localizer = localizer;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = _localizer["Po_AuthorityTitle"].Value;
        ViewData["Section"] = "Administration";
        ViewBag.Roles = await _context.Roles.AsNoTracking().OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync();
        return View(await _service.GetRulesAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, int level, decimal minAmount, decimal? maxAmount, string currency, string approverRole,
        string? description, bool isActive = false)
    {
        var r = await _service.SaveRuleAsync(new PoAuthorityRuleInput(id, level, minAmount, maxAmount, currency ?? string.Empty,
            approverRole ?? string.Empty, description, isActive), new PoActor(null, _currentUser.Roles, _currentUser.UserName));
        TempData[r.Success ? "Message" : "Error"] = _localizer[r.Success ? "Po_RuleSaved" : r.Error!].Value;
        return RedirectToAction(nameof(Index));
    }
}
