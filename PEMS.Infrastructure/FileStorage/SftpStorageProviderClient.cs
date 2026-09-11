using PEMS.Application.DTOs;
using PEMS.Application.Exceptions;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace PEMS.Infrastructure.FileStorage;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۹: پیاده‌سازی SFTP با SSH.NET (تنها
/// کتابخانه‌ی بالغ/نگهداری‌شده‌ی SFTP برای NET - پیش از این هیچ کد SFTP در پروژه وجود نداشت،
/// طبق ممیزی مستقیم این نشست). معماری برای PrivateKey/Passphrase آینده آماده است (بند ۹) -
/// فعلاً فقط Username/Password پیاده‌سازی شده، بدون نیاز به تغییر مدل Domain برای افزودن آن بعداً.
/// </summary>
public class SftpStorageProviderClient : IStorageProviderClient
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _rootPath;

    public SftpStorageProviderClient(string host, int port, string username, string password, string rootPath)
    {
        _host = host;
        _port = port <= 0 ? 22 : port;
        _username = username;
        _password = password;
        _rootPath = string.IsNullOrWhiteSpace(rootPath) ? "/" : rootPath;
    }

    private SftpClient CreateClient() => new(_host, _port, _username, _password)
    {
        ConnectionInfo = { Timeout = TimeSpan.FromSeconds(10) }
    };

    private string ResolveRemotePath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains(".."))
            throw new StorageException(StorageErrorCodes.StoragePathInvalid, "مسیر فایل درخواستی نامعتبر است.");

        return $"{_rootPath.TrimEnd('/')}/{normalized}";
    }

    private static void EnsureRemoteDirectory(SftpClient client, string remotePath)
    {
        var dir = remotePath[..remotePath.LastIndexOf('/')];
        if (string.IsNullOrEmpty(dir)) return;

        var parts = dir.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = "";
        foreach (var part in parts)
        {
            current += "/" + part;
            if (!client.Exists(current)) client.CreateDirectory(current);
        }
    }

    public async Task SaveAsync(string relativePath, Stream content, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            await client.ConnectAsync(cancellationToken);
            var remotePath = ResolveRemotePath(relativePath);
            EnsureRemoteDirectory(client, remotePath);
            await client.UploadFileAsync(content, remotePath, cancellationToken);
        }
        catch (SshAuthenticationException ex)
        {
            throw new StorageException(StorageErrorCodes.StorageAuthenticationFailed, "احراز هویت SFTP ناموفق بود.", ex);
        }
        catch (StorageException) { throw; }
        catch (Exception ex)
        {
            throw new StorageException(StorageErrorCodes.StorageWriteFailed, "آپلود فایل روی SFTP ناموفق بود.", ex);
        }
        finally
        {
            if (client.IsConnected) client.Disconnect();
        }
    }

    public async Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        // طبق بند ۲۷: کل فایل در حافظه بافر نمی‌شود - به یک فایل موقت دانلود می‌شود
        using var client = CreateClient();
        var tempPath = Path.Combine(Path.GetTempPath(), $"pems_sftp_{Guid.NewGuid()}.tmp");

        try
        {
            await client.ConnectAsync(cancellationToken);
            var remotePath = ResolveRemotePath(relativePath);

            if (!client.Exists(remotePath))
                throw new StorageException(StorageErrorCodes.StorageReadFailed, "فایل درخواستی روی SFTP یافت نشد.");

            await using (var tempStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                await client.DownloadFileAsync(remotePath, tempStream, cancellationToken);

            return new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
        }
        catch (SshAuthenticationException ex)
        {
            throw new StorageException(StorageErrorCodes.StorageAuthenticationFailed, "احراز هویت SFTP ناموفق بود.", ex);
        }
        catch (StorageException) { throw; }
        catch (Exception ex)
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw new StorageException(StorageErrorCodes.StorageUnavailable, "اتصال به سرور SFTP ناموفق بود.", ex);
        }
        finally
        {
            if (client.IsConnected) client.Disconnect();
        }
    }

    public async Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            await client.ConnectAsync(cancellationToken);
            return client.Exists(ResolveRemotePath(relativePath));
        }
        catch (Exception ex)
        {
            throw new StorageException(StorageErrorCodes.StorageUnavailable, "اتصال به سرور SFTP ناموفق بود.", ex);
        }
        finally
        {
            if (client.IsConnected) client.Disconnect();
        }
    }

    public async Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            await client.ConnectAsync(cancellationToken);
            var remotePath = ResolveRemotePath(relativePath);
            if (client.Exists(remotePath)) await client.DeleteFileAsync(remotePath, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new StorageException(StorageErrorCodes.StorageDeleteFailed, "حذف فایل از SFTP ناموفق بود.", ex);
        }
        finally
        {
            if (client.IsConnected) client.Disconnect();
        }
    }

    public async Task<StorageHealthResultDto> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var result = new StorageHealthResultDto();
        using var client = CreateClient();

        try
        {
            await client.ConnectAsync(cancellationToken);
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Connection", Pass = true });
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Authentication", Pass = true });
        }
        catch (SshAuthenticationException)
        {
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Connection", Pass = true });
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Authentication", Pass = false, Detail = "نام کاربری/رمز عبور نادرست است." });
            result.OverallStatus = StorageHealthStatuses.AuthenticationFailed;
            result.Message = "احراز هویت ناموفق بود.";
            return result;
        }
        catch (Exception)
        {
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Connection", Pass = false, Detail = "سرور در دسترس نیست یا زمان اتصال به پایان رسید." });
            result.OverallStatus = StorageHealthStatuses.Offline;
            result.Message = "اتصال به سرور SFTP ناموفق بود.";
            return result;
        }

        try
        {
            var rootExists = client.Exists(_rootPath);
            result.Steps.Add(new StorageHealthStepResultDto { Step = "RootPath", Pass = rootExists, Detail = _rootPath });
            if (!rootExists)
            {
                result.OverallStatus = StorageHealthStatuses.PermissionDenied;
                result.Message = "مسیر ریشه‌ی پیکربندی‌شده روی سرور یافت نشد.";
                client.Disconnect();
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
            EnsureRemoteDirectory(client, probeRemotePath);
            await using (var ms = new MemoryStream(new byte[] { 1, 2, 3 }))
                await client.UploadFileAsync(ms, probeRemotePath, cancellationToken);
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Write", Pass = true });
        }
        catch (Exception ex)
        {
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Write", Pass = false, Detail = ex.Message });
        }

        try
        {
            await using var readBuf = new MemoryStream();
            await client.DownloadFileAsync(probeRemotePath, readBuf, cancellationToken);
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Read", Pass = true });
        }
        catch (Exception ex)
        {
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Read", Pass = false, Detail = ex.Message });
        }

        try
        {
            if (client.Exists(probeRemotePath)) await client.DeleteFileAsync(probeRemotePath, cancellationToken);
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Delete", Pass = true });
        }
        catch (Exception ex)
        {
            result.Steps.Add(new StorageHealthStepResultDto { Step = "Delete", Pass = false, Detail = ex.Message });
        }

        client.Disconnect();

        result.OverallStatus = result.Steps.All(x => x.Pass) ? StorageHealthStatuses.Online : StorageHealthStatuses.PermissionDenied;
        return result;
    }
}
