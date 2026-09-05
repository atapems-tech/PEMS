using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

public class ChatAssistantController : Controller
{
    private readonly IChatAssistantService _chatService;
    private readonly ICurrentUserService _currentUser;

    public ChatAssistantController(IChatAssistantService chatService, ICurrentUserService currentUser)
    {
        _chatService = chatService;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Engineering Chat Assistant";
        ViewData["Section"] = "AI Engineering Intelligence";

        var userId = _currentUser.UserId ?? 0;
        var history = await _chatService.GetHistoryAsync(userId);

        return View(history);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask([FromForm] string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return BadRequest();

        var userId = _currentUser.UserId ?? 0;
        var answer = await _chatService.AskAsync(userId, message);

        return Json(new { answer });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear()
    {
        var userId = _currentUser.UserId ?? 0;
        await _chatService.ClearHistoryAsync(userId);
        return RedirectToAction(nameof(Index));
    }
}
