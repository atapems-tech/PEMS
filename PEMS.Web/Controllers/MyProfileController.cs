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
    public async Task<IActionResult> UploadPhoto(IFormFile file)
    {
        var employee = await GetMyEmployeeAsync();
        if (employee == null) return NotFound();

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "فایلی انتخاب نشده.";
            return RedirectToAction(nameof(Index));
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
        {
            TempData["Error"] = "فقط فایل تصویری (jpg/png/webp) مجاز است.";
            return RedirectToAction(nameof(Index));
        }

        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profile-photos");
        if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

        var storedName = $"emp{employee.EmployeeId}_{Guid.NewGuid()}{ext}";
        var physicalPath = Path.Combine(uploadPath, storedName);

        using (var stream = new FileStream(physicalPath, FileMode.Create))
            await file.CopyToAsync(stream);

        employee.PhotoPath = "/uploads/profile-photos/" + storedName;
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
    public async Task<IActionResult> SaveSignature(string signatureDataUrl)
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

        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "signatures");
        if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

        var storedName = $"emp{employee.EmployeeId}_{Guid.NewGuid()}.png";
        var physicalPath = Path.Combine(uploadPath, storedName);

        await System.IO.File.WriteAllBytesAsync(physicalPath, bytes);

        employee.SignaturePath = "/uploads/signatures/" + storedName;
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
