using System.Text;
using PEMS.Application.Exceptions;
using PEMS.Infrastructure.FileStorage;

namespace PEMS.Tests.FileStorage;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۳۰: تست‌های واقعی Save/Read/Exists/Delete
/// روی یک پوشه‌ی موقت واقعی، به‌علاوه‌ی کل فهرست موارد حفاظت در برابر Path Traversal که سند
/// صریحاً درخواست کرده است (../، ..\، مسیر مطلق ویندوز، UNC).
/// </summary>
public class FileSystemStorageProviderClientTests : IDisposable
{
    private readonly string _root;
    private readonly FileSystemStorageProviderClient _client;

    public FileSystemStorageProviderClientTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "pems_storage_test_" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
        _client = new FileSystemStorageProviderClient(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task SaveAsync_ThenOpenReadAsync_ReturnsSameContent()
    {
        var bytes = Encoding.UTF8.GetBytes("hello enterprise storage");
        await using (var content = new MemoryStream(bytes))
            await _client.SaveAsync("2026/1/file.txt", content);

        Assert.True(await _client.ExistsAsync("2026/1/file.txt"));

        await using var read = await _client.OpenReadAsync("2026/1/file.txt");
        using var reader = new StreamReader(read);
        var text = await reader.ReadToEndAsync();

        Assert.Equal("hello enterprise storage", text);
    }

    [Fact]
    public async Task DeleteAsync_ExistingFile_RemovesIt()
    {
        await using (var content = new MemoryStream(Encoding.UTF8.GetBytes("to be deleted")))
            await _client.SaveAsync("del/file.txt", content);

        Assert.True(await _client.ExistsAsync("del/file.txt"));
        await _client.DeleteAsync("del/file.txt");
        Assert.False(await _client.ExistsAsync("del/file.txt"));
    }

    [Fact]
    public async Task DeleteAsync_NonExistentFile_DoesNotThrow()
    {
        await _client.DeleteAsync("never/existed.txt");
    }

    [Fact]
    public async Task ExistsAsync_NonExistentFile_ReturnsFalse()
    {
        Assert.False(await _client.ExistsAsync("nope.txt"));
    }

    [Fact]
    public async Task OpenReadAsync_NonExistentFile_ThrowsStorageReadFailed()
    {
        var ex = await Assert.ThrowsAsync<StorageException>(() => _client.OpenReadAsync("missing.txt"));
        Assert.Equal(StorageErrorCodes.StorageReadFailed, ex.Code);
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("..\\secret.txt")]
    [InlineData("a/../../secret.txt")]
    [InlineData("a/b/../../../secret.txt")]
    public async Task SaveAsync_RelativeTraversalPath_ThrowsStoragePathInvalid(string maliciousPath)
    {
        await using var content = new MemoryStream(new byte[] { 1 });
        var ex = await Assert.ThrowsAsync<StorageException>(() => _client.SaveAsync(maliciousPath, content));
        Assert.Equal(StorageErrorCodes.StoragePathInvalid, ex.Code);
    }

    [Fact]
    public async Task SaveAsync_WindowsAbsolutePathEscapesRoot_ThrowsStoragePathInvalid()
    {
        var absolute = Path.Combine(Path.GetPathRoot(_root)!, "pems_outside_root_" + Guid.NewGuid() + ".txt");
        await using var content = new MemoryStream(new byte[] { 1 });

        var ex = await Assert.ThrowsAsync<StorageException>(() => _client.SaveAsync(absolute, content));
        Assert.Equal(StorageErrorCodes.StoragePathInvalid, ex.Code);
        Assert.False(File.Exists(absolute));
    }

    [Fact]
    public async Task SaveAsync_UncStylePath_IsNeutralizedIntoAHarmlessSubfolderInsideRoot()
    {
        // طبق ResolvePhysicalPath: بک‌اسلش‌ها به اسلش تبدیل و اسلش‌های ابتدایی حذف می‌شوند، پس
        // "\\SOME-OTHER-SERVER\share\escape.txt" به یک مسیر نسبی معمولی تبدیل می‌شود، نه یک UNC
        // واقعی - نتیجه هرگز از _root فراتر نمی‌رود (نه یک استثنا، بلکه یک زیرپوشه‌ی بی‌ضرر).
        const string uncStylePath = @"\\SOME-OTHER-SERVER\share\escape.txt";
        await using (var content = new MemoryStream(new byte[] { 1 }))
            await _client.SaveAsync(uncStylePath, content);

        var expectedPhysical = Path.Combine(_root, "SOME-OTHER-SERVER", "share", "escape.txt");
        Assert.True(File.Exists(expectedPhysical));
        Assert.StartsWith(Path.GetFullPath(_root), Path.GetFullPath(expectedPhysical), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveAsync_LegitimateNestedRelativePath_StaysInsideRoot()
    {
        await using var content = new MemoryStream(new byte[] { 1, 2, 3 });
        await _client.SaveAsync("Equipment/2026/42/abc.dat", content);

        var expectedPhysical = Path.Combine(_root, "Equipment", "2026", "42", "abc.dat");
        Assert.True(File.Exists(expectedPhysical));
    }

    [Fact]
    public async Task TestConnectionAsync_ValidRoot_AllStepsPass()
    {
        var result = await _client.TestConnectionAsync();

        Assert.Equal(Domain.Entities.StorageHealthStatuses.Online, result.OverallStatus);
        Assert.All(result.Steps, step => Assert.True(step.Pass, $"{step.Step} failed: {step.Detail}"));
    }
}
