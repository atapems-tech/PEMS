using PEMS.Application.Exceptions;
using PEMS.Infrastructure.FileStorage;

namespace PEMS.Tests.FileStorage;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۲۹: تست‌های FTP/SFTP نباید به یک سرور
/// واقعی نیاز داشته باشند (CI استاندارد نباید به آن وابسته باشد) - این تست‌ها عمداً یک هاست
/// غیرقابل‌حل (Unresolvable) هدف قرار می‌دهند تا رفتار "خطای شبکه به StorageException تبدیل
/// می‌شود، هرگز یک استثنای خام کتابخانه‌ی FluentFTP/SSH.NET به بیرون نشت نمی‌کند" را اثبات کنند -
/// بدون وابستگی به دسترس‌پذیری اینترنت یا سرور واقعی.
/// </summary>
public class RemoteStorageProviderClientTests
{
    private const string UnresolvableHost = "pems-nonexistent-host.invalid";

    [Fact]
    public async Task Ftp_SaveAsync_UnreachableHost_ThrowsStorageExceptionNotRawLibraryException()
    {
        var client = new FtpStorageProviderClient(UnresolvableHost, 21, "user", "pass", "/root", useSsl: false, usePassiveMode: true);

        await using var content = new MemoryStream(new byte[] { 1, 2, 3 });
        await Assert.ThrowsAsync<StorageException>(() => client.SaveAsync("file.txt", content));
    }

    [Fact]
    public async Task Ftp_TestConnectionAsync_UnreachableHost_ReturnsOfflineWithoutThrowing()
    {
        var client = new FtpStorageProviderClient(UnresolvableHost, 21, "user", "pass", "/root", useSsl: false, usePassiveMode: true);

        var result = await client.TestConnectionAsync();

        Assert.Equal(Domain.Entities.StorageHealthStatuses.Offline, result.OverallStatus);
        Assert.Contains(result.Steps, s => s.Step == "Connection" && !s.Pass);
    }

    [Fact]
    public async Task Sftp_SaveAsync_UnreachableHost_ThrowsStorageExceptionNotRawLibraryException()
    {
        var client = new SftpStorageProviderClient(UnresolvableHost, 22, "user", "pass", "/root");

        await using var content = new MemoryStream(new byte[] { 1, 2, 3 });
        await Assert.ThrowsAsync<StorageException>(() => client.SaveAsync("file.txt", content));
    }

    [Fact]
    public async Task Sftp_TestConnectionAsync_UnreachableHost_ReturnsOfflineWithoutThrowing()
    {
        var client = new SftpStorageProviderClient(UnresolvableHost, 22, "user", "pass", "/root");

        var result = await client.TestConnectionAsync();

        Assert.Equal(Domain.Entities.StorageHealthStatuses.Offline, result.OverallStatus);
        Assert.Contains(result.Steps, s => s.Step == "Connection" && !s.Pass);
    }
}
