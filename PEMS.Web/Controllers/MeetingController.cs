using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// مدیریت جلسات (طبق نمونه‌ی کارتابل مرجع).
/// </summary>
public class MeetingController : Controller
{
    private readonly IMeetingService _service;
    private readonly ITaskService _taskService;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public MeetingController(IMeetingService service, ITaskService taskService, PEMSDbContext context, ICurrentUserService currentUser)
    {
        _service = service;
        _taskService = taskService;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Meetings";
        ViewData["Section"] = "Correspondence Management";

        var meetings = await _service.GetAllAsync();

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        return View(meetings);
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "Meeting Details";
        ViewData["Section"] = "Correspondence Management";

        var detail = await _service.GetDetailAsync(id);
        if (detail == null) return NotFound();

        await LoadDropdownsAsync();

        return View(detail);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "New Meeting";
        ViewData["Section"] = "Correspondence Management";

        await LoadDropdownsAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string title, DateTime meetingDate, string? location,
        string? organizerName, int? projectId, string? agenda)
    {
        var id = await _service.CreateAsync(title, meetingDate, location, organizerName, projectId, agenda);
        TempData["Message"] = "Meeting scheduled";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMinutes(int meetingId, string minutesNotes, string status)
    {
        await _service.UpdateMinutesAsync(meetingId, minutesNotes, status);
        TempData["Message"] = "Minutes updated";
        return RedirectToAction(nameof(Details), new { id = meetingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAttendee(int meetingId, int? employeeId, string? externalName, bool isRequired = true)
    {
        await _service.AddAttendeeAsync(meetingId, employeeId, externalName, isRequired);
        return RedirectToAction(nameof(Details), new { id = meetingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAttended(int meetingAttendeeId, int meetingId, bool attended)
    {
        await _service.MarkAttendedAsync(meetingAttendeeId, attended);
        return RedirectToAction(nameof(Details), new { id = meetingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddActionItem(int meetingId, string title, int? assignedToEmployeeId, DateTime? dueDate)
    {
        await _taskService.CreateAsync(title, null, assignedToEmployeeId, dueDate, "Normal",
            "Meeting", meetingId, _currentUser.UserId, _currentUser.FullName);

        TempData["Message"] = "Action item added";
        return RedirectToAction(nameof(Details), new { id = meetingId });
    }

    private async Task LoadDropdownsAsync()
    {
        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Employees = await _context.Employees.Where(x => x.IsActive)
            .Select(x => new { x.EmployeeId, Name = x.FirstName + " " + x.LastName }).ToListAsync();
    }
}
