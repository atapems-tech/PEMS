using FluentFTP;
using FluentFTP.Exceptions;
using PEMS.Application.DTOs;
using PEMS.Application.Exceptions;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;

namespace PEMS.Infrastructure.FileStorage;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۸: پیاده‌سازی FTP عمومی و چند-Provider -
/// از همان کتابخانه‌ی بالغ/نگهداری‌شده FluentFTP استفاده می‌کند که پیش از این فقط برای VDR
/// (FtpUploadService موجود) به کار رفته بود؛ آن سرویس دست‌نخورده باقی می‌ماند (بند ۳۱) - این
/// یک Client عمومی جدید برای زیرساخت چند-Provider است، نه جایگزینی مستقیم آن.
/// </summary>
public class FtpStorageProviderClient : IStorageProviderClient
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _rootPath;
    private readonly bool _useSsl;
    private readonly bool _usePassiveMode;

    public FtpStorageProviderClient(string host, int port, string username, string password, string rootPath, bool useSsl, bool usePassiveMode)
    {
        _host = host;
        _port = port;
        _username = username;
        _password = password;
        _rootPath = string.IsNullOrWhiteSpace(rootPath) ? "/" : rootPath;
        _useSsl = useSsl;
        _usePassiveMode = usePassiveMode;
    }

    private AsyncFtpClient CreateClient()
    {
        var client = new AsyncFtpClient(_host, _username, _password, _port);
        if (_useSsl) client.Config.EncryptionMode = FtpEncryptionMode.Explicit;
        client.Config.DataConnectionType = _usePassiveMode ? FtpDataConnectionType.PASV : FtpDataConnectionType.PORT;
        client.Config.ConnectTimeout = 10000;
        client.Config.ReadTimeout = 30000;
        return client;
    }

    private string ResolveRemotePath(string relativePath)
    {
        // طبق بند ۱۰/۳۰: همان قاعده‌ی Path Traversal - هرگز به مسیر بیرون از RootPath اجازه نمی‌دهیم
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains(".."))
            throw new StorageException(StorageErrorCodes.StoragePathInvalid, "مسیر فایل درخواستی نامعتبر است.");

        return $"{_rootPath.TrimEnd('/')}/{normalized}";
    }

    public async Task SaveAsync(string relativePath, Stream content, CancellationToken cancellationToken = default)
    {
        await using var client = CreateClient();
        try
        {
            await client.Connect(cancellationToken);
            var remotePath = ResolveRemotePath(relativePath);
            var status = await client.UploadStream(content, remotePath, FtpRemoteExists.Overwrite, createRemoteDir: true, token: cancellationToken);
            if (status != FtpStatus.Success)
                throw new StorageException(StorageErrorCodes.StorageWriteFailed, "آپلود فایل روی FTP ناموفق بود.");
        }
        catch (FtpAuthenticationException ex)
        {
            throw new StorageException(StorageErrorCodes.StorageAuthenticationFailed, "احراز هویت FTP ناموفق بود.", ex);
        }
        catch (StorageException) { throw; }
        catch (Exception ex)
        {
            throw new StorageException(StorageErrorCodes.StorageUnavailable, "اتصال به سرور FTP ناموفق بود.", ex);
        }
        finally
        {
            if (client.IsConnected) await client.Disconnect(cancellationToken);
        }
    }

    public async Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        // طبق بند ۲۷: کل فایل در حافظه بافر نمی‌شود - به یک فایل موقت دانلود می‌شود و یک
        // FileStream با DeleteOnClose برگردانده می‌شود (بعد از خواندن، خودکار پاک می‌شود)
        await using var client = CreateClient();
        var tempPath = Path.Combine(Path.GetTempPath(), $"pems_ftp_{Guid.NewGuid()}.tmp");

        try
        {
            await client.Connect(cancellationToken);
            var remotePath = ResolveRemotePath(relativePath);

            await using (var tempStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                var status = await client.DownloadStream(tempStream, remotePath, token: cancellationToken);
                if (!status)
                    throw new StorageException(StorageErrorCodes.StorageReadFailed, "دانلود فایل از FTP ناموفق بود.");
            }

            return new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
        }
        catch (FtpAuthenticationException ex)
        {
            throw new StorageException(StorageErrorCodes.StorageAuthenticationFailed, "احراز هویت FTP ناموفق بود.", ex);
        }
        catch (StorageException) { throw; }
        catch (Exception ex)
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw new StorageException(StorageErrorCodes.StorageUnavailable, "اتصال به سرور FTP ناموفق بود.", ex);
        }
        finally
        {
            if (client.IsConnected) await client.Disconnect(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        await using var client = CreateClient();
        try
        {
            await client.Connect(cancellationToken);
            return await client.FileExists(ResolveRemotePath(relativePath), cancellationToken);
        }
        catch (Exception ex)
        {
            throw new StorageException(StorageErrorCodes.StorageUnavailable, "اتصال به سرور FTP ناموفق بود.", ex);
        }
        finally
        {
            if (client.IsConnected) await client.Disconnect(cancellationToken);
        }
    }

    public async Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        await using var client = CreateClient();
        try
        {
            await client.Connect(cancellationToken);
            var remotePath = ResolveRemotePath(relativePath);
            if (await client.FileExists(remotePath, cancellationToken))
                await client.DeleteFile(remotePath, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new StorageException(StorageErrorCodes.StorageDeleteFailed, "حذف فایل از FTP ناموفق بود.", ex);
        }
        finally
        {
            if (client.IsConnected) await client.Disconnect(cancellationToken);
        }
    }

    public async Task<StorageHealthResultDto> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var result = new StorageHealthResultDto();
        await using var client = CreateClient();

        try
        {
            await client.Connect(cancellationToken);
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Connection", Pass = true });
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Authentication", Pass = true });
        }
        catch (FtpAuthenticationException ex)
        {
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Connection", Pass = true });
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Authentication", Pass = false, Detail = "نام کاربری/رمز عبور نادرست است." });
            result.OverallStatus = StorageHealthStatuses.AuthenticationFailed;
            result.Message = "احراز هویت ناموفق بود.";
            return result;
        }
        catch (Exception ex)
        {
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Connection", Pass = false, Detail = "سرور در دسترس نیست یا زمان اتصال به پایان رسید." });
            result.OverallStatus = StorageHealthStatuses.Offline;
            result.Message = "اتصال به سرور FTP ناموفق بود.";
            return result;
        }

        try
        {
            var rootExists = await client.DirectoryExists(_rootPath, cancellationToken);
            result.Steps.Add(new StorageHealthStepResultDto { Step = "RootPath", Pass = rootExists, Detail = _rootPath });
            if (!rootExists)
            {
                result.OverallStatus = StorageHealthStatuses.PermissionDenied;
                result.Message = "مسیر ریشه‌ی پیکربندی‌شده روی سرور یافت نشد.";
                await client.Disconnect(cancellationToken);
                return result;
            }
        }
        catch (Exception ex)
        {
            result.Steps.Add(new StorageHealthStepResultDto { Step = "RootPath", Pass = false, Detail = ex.Message });
        }

        var probeRelativePath = $"__pems_healthcheck__/{Guid.NewGuid()}.tmp";
        var probeRemotePath = ResolveRemotePath(probeRelativePath);

        try
        {
            await using (var ms = new MemoryStream(new byte[] { 1, 2, 3 }))
            {
                var status = await client.UploadStream(ms, probeRemotePath, FtpRemoteExists.Overwrite, createRemoteDir: true, token: cancellationToken);
                result.Steps.Add(new StorageHealthStepResultDto { Step = "Write", Pass = status == FtpStatus.Success });
            }
        }
        catch (Exception ex)
        {
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Write", Pass = false, Detail = ex.Message });
        }

        try
        {
            await using var readBuf = new MemoryStream();
            var readOk = await client.DownloadStream(readBuf, probeRemotePath, token: cancellationToken);
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Read", Pass = readOk });
        }
        catch (Exception ex)
        {
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Read", Pass = false, Detail = ex.Message });
        }

        try
        {
            if (await client.FileExists(probeRemotePath, cancellationToken))
                await client.DeleteFile(probeRemotePath, cancellationToken);
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Delete", Pass = true });
        }
        catch (Exception ex)
        {
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Delete", Pass = false, Detail = ex.Message });
        }

        await client.Disconnect(cancellationToken);

        result.OverallStatus = result.Steps.All(x => x.Pass) ? StorageHealthStatuses.Online : StorageHealthStatuses.PermissionDenied;
        return result;
    }
}
