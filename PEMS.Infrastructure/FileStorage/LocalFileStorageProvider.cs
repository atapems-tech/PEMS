using PEMS.Application.Interfaces;

namespace PEMS.Infrastructure.FileStorage;

/// <summary>
/// طبق ممیزی امنیتی (P0 - Engineering File Security Containment): پیاده‌سازی محلی
/// IFileStorageService با ریشه‌ی ذخیره‌سازی عمداً بیرون از wwwroot - این دقیقاً همان تغییری است
/// که آسیب‌پذیری اصلی را می‌بندد (UseStaticFiles فقط داخل wwwroot سرویس می‌دهد؛ اگر بایت‌ها اصلاً
/// آن‌جا نباشند، هیچ URL مستقیمی نمی‌تواند به آن‌ها برسد، صرف‌نظر از تنظیمات Middleware).
///
/// سازگاری با داده‌ی موجود: ردیف‌های قدیمی DocumentAttachment.FilePath به شکل
/// "/uploads/vdr/comments/x.pdf" ذخیره شده‌اند (از دورانی که FilePath یک URL عمومی بود، نه
/// یک Key ذخیره‌سازی). این پیاده‌سازی پیشوند "/uploads/" را در ورودی هر متد نادیده می‌گیرد تا
/// همان مقادیر قدیمی، بدون هیچ UPDATE روی دیتابیس، به مسیر فیزیکی جدید (زیر ریشه‌ی امن) نگاشت
/// شوند - فقط فایل‌های فیزیکی به ریشه‌ی جدید کپی می‌شوند، نه ردیف‌های دیتابیس.
/// </summary>
public class LocalFileStorageProvider : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageProvider(string rootPath)
    {
        _rootPath = rootPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(string logicalFolder, string originalFileName, Stream content)
    {
        var safeFolder = NormalizeFolder(logicalFolder);
        var storedName = $"{Guid.NewGuid()}{Path.GetExtension(originalFileName)}";
        var key = $"{safeFolder}/{storedName}";

        var physicalPath = ResolvePhysicalPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

        // CreateNew نه Create - هرگز نباید بی‌صدا رونویسی کند؛ چون storedName خودمان GUID تولید
        // می‌کنیم، برخورد عملاً غیرممکن است، ولی اگر رخ داد باید صریح خطا بدهد نه رونویسی خاموش
        await using var fs = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(fs);

        return key;
    }

    public Task<Stream> OpenReadAsync(string key)
    {
        var physicalPath = ResolvePhysicalPath(key);
        if (!File.Exists(physicalPath))
            throw new FileNotFoundException("فایل ذخیره‌شده یافت نشد.", key);

        Stream stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string key)
    {
        return Task.FromResult(File.Exists(ResolvePhysicalPath(key)));
    }

    public Task DeleteAsync(string key)
    {
        var physicalPath = ResolvePhysicalPath(key);
        if (File.Exists(physicalPath))
            File.Delete(physicalPath);
        return Task.CompletedTask;
    }

    private static string NormalizeFolder(string logicalFolder) =>
        logicalFolder.Trim('/', '\\').Replace('\\', '/');

    // طبق کامنت بالای کلاس: پیشوند قدیمی "/uploads/" را نادیده می‌گیرد تا مقادیر موجود
    // دیتابیس بدون تغییر کار کنند
    private string ResolvePhysicalPath(string key)
    {
        var normalized = key.Replace('\\', '/').TrimStart('/');
        if (normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["uploads/".Length..];

        // طبق بند صریح ممیزی (Path Traversal): حتی اگر یک Key ناسالم از جایی نشت کند، حاصل
        // نهایی باید همیشه زیرمجموعه‌ی _rootPath بماند - GetFullPath هر ".." را حل می‌کند و اینجا
        // نتیجه با ریشه مقایسه می‌شود، نه صرفاً اعتماد به ورودی
        var combined = Path.GetFullPath(Path.Combine(_rootPath, normalized));
        var rootFull = Path.GetFullPath(_rootPath);

        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("مسیر فایل درخواستی خارج از محدوده‌ی مجاز ذخیره‌سازی است.");

        return combined;
    }
}
