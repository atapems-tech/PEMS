namespace PEMS.Application.Interfaces;

/// <summary>A refusal (<c>Success</c> false, <c>Error</c> the reason) or the signed package to download.</summary>
public sealed class PackageFileResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public byte[]? Bytes { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/zip";
}

/// <summary>
/// The vendor's package panel (SCRUM-584) as operations: build and sign an install, update or feed package and download it. Signing needs the vendor key, which is configuration of the vendor's own deployment only
/// (<c>Migrator:SigningKey</c>, a PKCS#8 private key in base64, and <c>Migrator:SignerKeyId</c>, set in the secrets env file of that machine, never in the repository and never on a customer's installation); without it every
/// operation refuses. A built package is verified against the matching public key before it is handed out, so the migrator center accepts it and refuses a changed copy (SCRUM-581).
/// </summary>
public interface IPackageBuilder
{
    /// <summary>A feed package: <paramref name="rowsJson"/> is a JSON array of objects (field name to text) for <paramref name="entity"/>, applied in <paramref name="mode"/> (append, overwrite or merge).</summary>
    [PEMS.Application.Common.PemsOperation("Packager", "BuildFeed", Roles = "SystemAdmin")]
    Task<PackageFileResult> BuildFeedAsync(string toVersion, string entity, string mode, string rowsJson);

    /// <summary>A first-installation package: the definitions listed in <paramref name="definitionRefs"/> (appKey:kind:key, comma separated) and the schema level.</summary>
    [PEMS.Application.Common.PemsOperation("Packager", "BuildInstall", Roles = "SystemAdmin")]
    Task<PackageFileResult> BuildInstallAsync(string toVersion, string schemaLevel, string? definitionRefs);

    /// <summary>An update package from <paramref name="fromVersion"/> to <paramref name="toVersion"/>: the schema level and the definitions that changed.</summary>
    [PEMS.Application.Common.PemsOperation("Packager", "BuildUpdate", Roles = "SystemAdmin")]
    Task<PackageFileResult> BuildUpdateAsync(string fromVersion, string toVersion, string schemaLevel, string? definitionRefs);
}
