# IFileStorageService — Design Only (Not Wired Into the App)

Per the P1 File Storage Security Review, this is a proposed compatibility-layer design, produced
for review — **no code from this file has been added to the running application.**

## Interface

```csharp
namespace PEMS.Application.Interfaces;

public interface IFileStorageService
{
    // logicalFolder ~ today's "mrs-import" / "engineering-comments" / "vdr" etc.
    // Returns a StoredFileRef, never a raw servable URL — callers must go through
    // IAuthorizedFileDownloadService (or an equivalent controller action) to fetch bytes.
    Task<StoredFileRef> SaveAsync(string logicalFolder, string originalFileName, Stream content, string? contentType = null);

    Task<Stream> OpenReadAsync(StoredFileRef reference);

    Task DeleteAsync(StoredFileRef reference);

    Task<bool> ExistsAsync(StoredFileRef reference);
}

public class StoredFileRef
{
    public string Provider { get; set; } = "local"; // "local" | "s3" | "azure-blob" ...
    public string Key { get; set; } = "";            // opaque - provider-specific (path, or blob key)
    public long SizeBytes { get; set; }
    public string? ContentType { get; set; }
}
```

## LocalFileStorageProvider (drop-in replacement for today's ad-hoc per-controller code)

```csharp
namespace PEMS.Persistence.Services;

public class LocalFileStorageProvider : IFileStorageService
{
    private readonly string _root; // e.g. Path.Combine(env.ContentRootPath, "filestore")
                                    // NOTE: deliberately NOT wwwroot - see rationale below

    public async Task<StoredFileRef> SaveAsync(string logicalFolder, string originalFileName, Stream content, string? contentType = null)
    {
        var storedName = $"{Guid.NewGuid()}{Path.GetExtension(originalFileName)}";
        var relativeKey = Path.Combine(logicalFolder, storedName).Replace('\\', '/');
        var physicalPath = Path.Combine(_root, relativeKey);
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

        await using var fs = new FileStream(physicalPath, FileMode.CreateNew); // CreateNew, not Create - never silently overwrite
        await content.CopyToAsync(fs);

        return new StoredFileRef { Provider = "local", Key = relativeKey, SizeBytes = fs.Length, ContentType = contentType };
    }

    public Task<Stream> OpenReadAsync(StoredFileRef reference) =>
        Task.FromResult<Stream>(new FileStream(Path.Combine(_root, reference.Key), FileMode.Open, FileAccess.Read));

    // ...DeleteAsync/ExistsAsync omitted for brevity, same pattern
}
```

## The one change this design requires that IS a real fix (flagged, not applied here)

The **root cause** of the P1 security finding is not the storage mechanism itself — it's that
files are saved under `wwwroot/`, which `app.UseStaticFiles()` serves unconditionally to anyone,
before authentication even runs. `LocalFileStorageProvider._root` must point **outside**
`wwwroot` (e.g. a sibling `filestore/` folder, or any path `UseStaticFiles()` doesn't cover) so
that the *only* way to reach a byte of file content is through an authenticated controller action
that calls `IFileStorageService.OpenReadAsync` and streams it back via `File()` — never a direct
static URL. This single change (root folder location) is what actually closes the exposure;
migrating to S3/Blob later is a separate, independent step once this interface exists, since blob
storage is never web-servable by accident in the same way a local folder under `wwwroot` is.

## Migration path once approved

1. Introduce `IFileStorageService`/`LocalFileStorageProvider`, register in DI, **root outside `wwwroot`**.
2. Add one authenticated `DownloadController` action (or extend the existing `DownloadAttachment`
   pattern already used by `EngineeringDocumentsController`) as the *only* path to file bytes.
3. Migrate the 6 controllers currently doing raw `Path.Combine(..., "wwwroot", "uploads", ...)`
   to call `IFileStorageService.SaveAsync` instead, one controller at a time, each verified.
4. Existing `DocumentAttachment.FilePath` values (already-uploaded files) need a one-time data
   migration to move bytes out of `wwwroot/uploads` and record the new `StoredFileRef.Key` — this
   is data movement, not schema change, and needs its own careful, separately-reviewed plan.
5. Only after (1)-(4) are stable: introduce an `S3FileStorageProvider`/`AzureBlobFileStorageProvider`
   as an alternative `IFileStorageService` implementation, switchable via configuration — the
   controllers and `DocumentAttachment` schema do not need to change again for this step.
