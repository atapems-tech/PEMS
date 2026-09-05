using FluentFTP;
using Microsoft.Extensions.Configuration;
using PEMS.Application.Interfaces;

namespace PEMS.Persistence.Services;

/// <summary>
/// پیاده‌سازی واقعی آپلود روی FTP Server طبق بخش ۵.۲ سند، با تنظیمات از appsettings.json
/// (کلید "Ftp") - Host/Username/Password باید توسط کاربر برای محیط واقعی پر شوند؛ در این نسخه
/// خالی هستند (نمی‌توان سرور FTP واقعی حدس زد).
/// </summary>
public class FtpUploadService : IFtpUploadService
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _baseRemotePath;
    private readonly bool _useFtps;

    public FtpUploadService(IConfiguration configuration)
    {
        _host = configuration["Ftp:Host"] ?? string.Empty;
        _port = configuration.GetValue<int?>("Ftp:Port") ?? 21;
        _username = configuration["Ftp:Username"] ?? string.Empty;
        _password = configuration["Ftp:Password"] ?? string.Empty;
        _baseRemotePath = configuration["Ftp:BaseRemotePath"] ?? "/";
        _useFtps = configuration.GetValue<bool?>("Ftp:UseFtps") ?? false;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_host) && !string.IsNullOrWhiteSpace(_username);

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string remoteFolder)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "تنظیمات سرور FTP (Ftp:Host / Ftp:Username) در appsettings.json خالی است - ابتدا آن‌ها را برای محیط واقعی پر کنید.");

        using var client = new AsyncFtpClient(_host, _username, _password, _port);

        if (_useFtps)
            client.Config.EncryptionMode = FtpEncryptionMode.Explicit;

        await client.Connect();

        try
        {
            var remotePath = $"{_baseRemotePath.TrimEnd('/')}/{remoteFolder.Trim('/')}/{fileName}";

            await client.UploadStream(fileStream, remotePath, FtpRemoteExists.Overwrite, createRemoteDir: true);

            return remotePath;
        }
        finally
        {
            await client.Disconnect();
        }
    }
}
