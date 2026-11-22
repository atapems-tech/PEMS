using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// نقطه‌ی ورود آپلود، طبق AI Engineering Intelligence Redesign: Upload دیگر هرگز مستقیم
/// EngineeringDocument نمی‌سازد. هر فایل وارد AI Intake Queue می‌شود، AI آن را طبقه‌بندی و
/// مسیریابی می‌کند (یا برای تایید انسانی در Intake Queue می‌ماند).
/// </summary>
public class DocumentUploadController : Controller
{
    private readonly IIntakeService _intakeService;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentUploadController> _logger;

    public DocumentUploadController(
        IIntakeService intakeService,
        PEMSDbContext context,
        ICurrentUserService currentUser,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentUploadController> logger)
    {
        _intakeService = intakeService;
        _context = context;
        _currentUser = currentUser;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Import Center parity: this action has no module permission, so enforce the same role gate that shows the Import Center card
        if (!PEMS.Web.Security.ImportCatalog.CanUse("DocumentUpload", User))
            return Forbid();

        ViewData["Title"] = "Document Upload";
        ViewData["Section"] = "AI Engineering Intelligence";

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        return View();
    }

}
