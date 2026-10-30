using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// ==============================================================================
/// پورتال شخصی کارمند - طبق درخواست: هر کاربر می‌تواند عکس پروفایل و امضای دیجیتال
/// (ترسیمی، نه فایل آپلودی) خودش را ثبت کند. امضا در جاهایی از سیستم که نیاز به نمایش
/// امضای تاییدکننده دارند (مثلاً Checked/Approved By) قابل‌استفاده است.
/// ==============================================================================
/// </summary>
[Authorize]
public class MyProfileController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public MyProfileController(PEMSDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "پروفایل من";
        ViewData["Section"] = "Account";

        var employee = await GetMyEmployeeAsync();
        if (employee == null)
        {
            TempData["Error"] = "کارمند مرتبط با این حساب کاربری یافت نشد - برای ثبت عکس/امضا باید حساب شما به یک رکورد Employee وصل باشد.";
            return View((PEMS.Domain.Entities.Employee?)null);
        }

        return View(employee);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_000_000)]
    [PEMS.Web.Security.Uploads.UploadPolicy(PEMS.Application.Uploads.UploadProfile.Image)]
    public async Task<IActionResult> UploadPhoto(IFormFile file, [FromServices] PEMS.Application.Uploads.IProtectedImageStore images)
    {
        var employee = await GetMyEmployeeAsync();
        if (employee == null) return NotFound();

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "فایلی انتخاب نشده.";
            return RedirectToAction(nameof(Index));
        }

        // SCRUM-202: type, size and content are checked by the upload guard; stored outside wwwroot, served by Files/Image (login required)
        await using (var stream = file.OpenReadStream())
            employee.PhotoPath = await images.SaveAsync(PEMS.Application.Uploads.ProtectedImageStore.ProfilePhotos, file.FileName, stream);
        await _context.SaveChangesAsync();

        TempData["Message"] = "عکس پروفایل ذخیره شد.";
        return RedirectToAction(nameof(Index));
    }

    // ==============================================================================
    // امضای دیجیتال ترسیمی: کلاینت با یک <canvas> امضا را می‌کشد و به‌صورت PNG (Data URL)
    // می‌فرستد - نه یک فایل آپلودی. این‌جا فقط رشته‌ی Base64 را به فایل واقعی تبدیل می‌کنیم.
    // ==============================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> SaveSignature(string signatureDataUrl, [FromServices] PEMS.Application.Uploads.IProtectedImageStore images)
    {
        var employee = await GetMyEmployeeAsync();
        if (employee == null) return NotFound();

        if (string.IsNullOrWhiteSpace(signatureDataUrl) || !signatureDataUrl.StartsWith("data:image/png;base64,"))
        {
            TempData["Error"] = "امضا خالی است یا فرمتش نامعتبره - دوباره امضا کن.";
            return RedirectToAction(nameof(Index));
        }

        byte[] bytes;
        try
        {
            var base64 = signatureDataUrl["data:image/png;base64,".Length..];
            bytes = Convert.FromBase64String(base64);
        }
        catch
        {
            TempData["Error"] = "امضا قابل‌پردازش نبود - دوباره تلاش کن.";
            return RedirectToAction(nameof(Index));
        }

        // SCRUM-202: the drawn signature is not a form file, so it is checked here with the same allowlist (PNG bytes)
        if (!PEMS.Application.Uploads.UploadPolicy.SignatureMatches(".png", bytes.AsSpan(0, Math.Min(bytes.Length, PEMS.Application.Uploads.UploadPolicy.HeadLength))))
        {
            TempData["Error"] = "امضا قابل‌پردازش نبود - دوباره تلاش کن.";
            return RedirectToAction(nameof(Index));
        }

        using (var stream = new MemoryStream(bytes))
            employee.SignaturePath = await images.SaveAsync(PEMS.Application.Uploads.ProtectedImageStore.Signatures, "signature.png", stream);
        await _context.SaveChangesAsync();

        TempData["Message"] = "امضای دیجیتال ذخیره شد.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<PEMS.Domain.Entities.Employee?> GetMyEmployeeAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;

        var employeeId = await _context.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();

        if (!employeeId.HasValue) return null;

        return await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId.Value);
    }
}
