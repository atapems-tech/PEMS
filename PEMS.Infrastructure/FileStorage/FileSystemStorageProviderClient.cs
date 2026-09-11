using PEMS.Application.DTOs;
using PEMS.Application.Exceptions;
using PEMS.Application.Interfaces;

namespace PEMS.Infrastructure.FileStorage;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۱۰/۱۱: پیاده‌سازی مشترک برای Local و
/// NetworkShare - از دید .NET، یک مسیر UNC (\\SERVER01\PEMS\Documents) و یک مسیر محلی
/// (D:\PEMS\Storage) هر دو فقط رشته‌های Path/Directory/File معمولی هستند؛ هیچ تفاوت کدی واقعی
/// بین این دو نوع Provider وجود ندارد، فقط برچسب پیکربندی. همان منطق اثبات‌شده‌ی حفاظت در برابر
/// Path Traversal LocalFileStorageProvider موجود این‌جا مستقل بازپیاده‌سازی شده (نه فراخوانی آن
/// کلاس، چون قرارداد Interface فرق دارد و LocalFileStorageProvider موجود نباید برای این ویژگی
/// جدید تغییر کند - بند ۳۱: «Do not rewrite existing upload/document modules unnecessarily»).
/// </summary>
public class FileSystemStorageProviderClient : IStorageProviderClient
{
    private readonly string _rootPath;

    public FileSystemStorageProviderClient(string rootPath)
    {
        _rootPath = rootPath;
    }

    public async Task SaveAsync(string relativePath, Stream content, CancellationToken cancellationToken = default)
    {
        var physicalPath = ResolvePhysicalPath(relativePath);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
            await using var fs = new FileStream(physicalPath, FileMode.Create, FileAccess.Write);
            await content.CopyToAsync(fs, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new StorageException(StorageErrorCodes.StoragePermissionDenied, "دسترسی نوشتن به مسیر ذخیره‌سازی وجود ندارد.", ex);
        }
        catch (IOException ex)
        {
            throw new StorageException(StorageErrorCodes.StorageWriteFailed, "نوشتن فایل روی ذخیره‌سازی ناموفق بود.", ex);
        }
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var physicalPath = ResolvePhysicalPath(relativePath);
        if (!File.Exists(physicalPath))
            throw new StorageException(StorageErrorCodes.StorageReadFailed, "فایل درخواستی روی ذخیره‌سازی یافت نشد.");

        try
        {
            Stream stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult(stream);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new StorageException(StorageErrorCodes.StoragePermissionDenied, "دسترسی خواندن از مسیر ذخیره‌سازی وجود ندارد.", ex);
        }
        catch (IOException ex)
        {
            throw new StorageException(StorageErrorCodes.StorageReadFailed, "خواندن فایل از ذخیره‌سازی ناموفق بود.", ex);
        }
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(ResolvePhysicalPath(relativePath)));

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var physicalPath = ResolvePhysicalPath(relativePath);
        try
        {
            if (File.Exists(physicalPath)) File.Delete(physicalPath);
            return Task.CompletedTask;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new StorageException(StorageErrorCodes.StoragePermissionDenied, "دسترسی حذف از مسیر ذخیره‌سازی وجود ندارد.", ex);
        }
        catch (IOException ex)
        {
            throw new StorageException(StorageErrorCodes.StorageDeleteFailed, "حذف فایل از ذخیره‌سازی ناموفق بود.", ex);
        }
    }

    public async Task<StorageHealthResultDto> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var result = new StorageHealthResultDto();
        var probeRelativePath = $"__pems_healthcheck__/{Guid.NewGuid()}.tmp";

        // Connection/Authentication برای Local/NetworkShare معنای شبکه‌ای FTP/SFTP را ندارند -
        // معادل آن‌ها همان دسترس‌پذیری خودِ مسیر ریشه است
        try
        {
            Directory.CreateDirectory(_rootPath);
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Connection", Pass = true });
            result.Steps.Add(new StorageHealthStepResultDto { Step = "RootPath", Pass = true, Detail = _rootPath });
        }
        catch (Exception ex)
        {
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Connection", Pass = false, Detail = ex.Message });
            result.OverallStatus = Domain.Entities.StorageHealthStatuses.Offline;
            result.Message = "مسیر ریشه در دسترس نیست.";
            return result;
        }

        try
        {
            await using (var ms = new MemoryStream(new byte[] { 1, 2, 3 }))
                await SaveAsync(probeRelativePath, ms, cancellationToken);
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Write", Pass = true });
        }
        catch (Exception ex)
        {
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Write", Pass = false, Detail = ex.Message });
            result.OverallStatus = Domain.Entities.StorageHealthStatuses.PermissionDenied;
            result.Message = "نوشتن آزمایشی ناموفق بود.";
            return result;
        }

        try
        {
            await using var read = await OpenReadAsync(probeRelativePath, cancellationToken);
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Read", Pass = true });
        }
        catch (Exception ex)
        {
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Read", Pass = false, Detail = ex.Message });
        }

        try
        {
            await DeleteAsync(probeRelativePath, cancellationToken);
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Delete", Pass = true });
        }
        catch (Exception ex)
        {
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Delete", Pass = false, Detail = ex.Message });
        }

        result.OverallStatus = result.Steps.All(x => x.Pass) ? Domain.Entities.StorageHealthStatuses.Online : Domain.Entities.StorageHealthStatuses.PermissionDenied;
        return result;
    }

    private string ResolvePhysicalPath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');

        // طبق بند ۱۰/۳۰: حتی اگر یک relativePath ناسالم از جایی نشت کند، حاصل نهایی باید همیشه
        // زیرمجموعه‌ی _rootPath بماند - GetFullPath هر ".." را حل می‌کند و نتیجه با ریشه مقایسه
        // می‌شود، نه صرفاً اعتماد به ورودی
        var combined = Path.GetFullPath(Path.Combine(_rootPath, normalized));
        var rootFull = Path.GetFullPath(_rootPath);

        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new StorageException(StorageErrorCodes.StoragePathInvalid, "مسیر فایل درخواستی خارج از محدوده‌ی مجاز ذخیره‌سازی است.");

        return combined;
    }
}
