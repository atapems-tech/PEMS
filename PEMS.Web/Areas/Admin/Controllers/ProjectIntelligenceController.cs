using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Persistence.Identity;
using PEMS.Web.Areas.Admin.Models;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// نمایش خروجی scripts/project-intelligence/Generate-ProjectIntelligence.ps1 - طبق
/// PEMS_PROJECT_INTELLIGENCE_SPEC.md: یک سیستم evidence-based (نه درصد پیشرفت دستی)
/// که وضعیت واقعی هر ماژول را از کد، DbContext، Migration ها، View ها، منو، تست‌ها،
/// مستندات و تاریخچه‌ی Git استخراج می‌کند. این کنترلر فقط JSON تولیدشده را می‌خواند؛
/// خودِ محاسبه در اسکریپت PowerShell انجام می‌شود تا اسکن کد/Git داخل پراسس وب اجرا نشود.
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class ProjectIntelligenceController : Controller
{
    private readonly IWebHostEnvironment _env;

    public ProjectIntelligenceController(IWebHostEnvironment env)
    {
        _env = env;
    }

    public IActionResult Index()
    {
        ViewData["Title"] = "Project Intelligence";
        ViewData["Section"] = "Administration";

        var reportPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "graphify-out", "project-intelligence.json"));

        if (!System.IO.File.Exists(reportPath))
        {
            ViewBag.ReportMissing = true;
            ViewBag.ReportPath = reportPath;
            return View(new ProjectIntelligenceReport());
        }

        ProjectIntelligenceReport report;
        try
        {
            var json = System.IO.File.ReadAllText(reportPath);
            report = JsonSerializer.Deserialize<ProjectIntelligenceReport>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new ProjectIntelligenceReport();
        }
        catch (JsonException)
        {
            ViewBag.ReportMissing = true;
            ViewBag.ReportPath = reportPath;
            ViewBag.ParseError = true;
            return View(new ProjectIntelligenceReport());
        }

        ViewBag.ReportMissing = false;
        ViewBag.ReportPath = reportPath;

        return View(report);
    }

    // اجرای دستی مولد از داخل UI - چون این خودِ اسکن (Git log روی هر فایل + پیمایش دیسک) کند
    // است و نباید داخل مسیر یک درخواست معمولی HTTP یا زمان‌بندی خودکار (بدون تأیید کاربر)
    // اجرا شود، فقط با کلیک صریح SystemAdmin روی این دکمه اجرا می‌شود.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refresh()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(_env.ContentRootPath, ".."));
        var scriptPath = Path.Combine(repoRoot, "scripts", "project-intelligence", "Generate-ProjectIntelligence.ps1");

        if (!System.IO.File.Exists(scriptPath))
        {
            TempData["Error"] = $"Generator script not found: {scriptPath}";
            return RedirectToAction(nameof(Index));
        }

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
            {
                TempData["Error"] = "Could not start the generator process.";
                return RedirectToAction(nameof(Index));
            }

            // خواندن استریم‌ها همزمان با انتظار برای پایان - وگرنه اگر خروجی از بافر Pipe
            // بیشتر بشه، پراسس گیر می‌کنه (Deadlock کلاسیک Process.Start در دات‌نت)
            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* بهترین تلاش برای Kill */ }
                TempData["Error"] = "Generator timed out after 3 minutes.";
                return RedirectToAction(nameof(Index));
            }

            var stderr = await stdErrTask;

            if (process.ExitCode != 0)
            {
                TempData["Error"] = string.IsNullOrWhiteSpace(stderr)
                    ? $"Generator exited with code {process.ExitCode}."
                    : $"Generator exited with code {process.ExitCode}: {stderr.Trim()}";
            }
            else
            {
                TempData["Message"] = "Report regenerated.";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to run generator: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}
