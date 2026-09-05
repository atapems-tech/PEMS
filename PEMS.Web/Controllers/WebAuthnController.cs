using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق سند PEMS-SEC-PERMIT-AUTH-001 بند ۲: نقاط پایانی JSON برای مراسم ثبت/تایید WebAuthn.
/// این کنترلر هیچ داده‌ی بیومتریکی نمی‌بیند - فقط Options تولیدشده توسط IWebAuthnService را به
/// کلاینت می‌دهد و پاسخ رمزنگاری‌شده‌ی مرورگر را برای تایید عبور می‌دهد.
/// </summary>
[Authorize]
public class WebAuthnController : Controller
{
    private readonly IWebAuthnService _webAuthnService;
    private readonly ICurrentUserService _currentUser;

    public WebAuthnController(IWebAuthnService webAuthnService, ICurrentUserService currentUser)
    {
        _webAuthnService = webAuthnService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetRegistrationOptions()
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();

        var optionsJson = await _webAuthnService.GetRegistrationOptionsJsonAsync(
            _currentUser.UserId.Value, _currentUser.UserName ?? "user", _currentUser.FullName ?? _currentUser.UserName ?? "User");

        return Content(optionsJson, "application/json");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteRegistration([FromForm] string attestationResponseJson, [FromForm] string optionsJson, [FromForm] string? nickname)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();

        var (success, error, _) = await _webAuthnService.CompleteRegistrationAsync(
            _currentUser.UserId.Value, attestationResponseJson, optionsJson, nickname);

        return Json(new { success, error });
    }

    [HttpGet]
    public async Task<IActionResult> GetAssertionOptions()
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();

        var optionsJson = await _webAuthnService.GetAssertionOptionsJsonAsync(_currentUser.UserId.Value);
        if (optionsJson == null) return NotFound(new { error = "هیچ Passkey ای برای این حساب ثبت نشده." });

        return Content(optionsJson, "application/json");
    }
}
