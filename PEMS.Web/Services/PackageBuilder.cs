using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PEMS.Application.Definitions;
using PEMS.Application.Interfaces;
using PEMS.Application.Migration;

namespace PEMS.Web.Services;

/// <summary>The vendor package panel operations (SCRUM-584); see <see cref="IPackageBuilder"/>.</summary>
public sealed class PackageBuilder(IConfiguration configuration, IDefinitionService definitions, ILogger<PackageBuilder> log) : IPackageBuilder
{
    public Task<PackageFileResult> BuildFeedAsync(string toVersion, string entity, string mode, string rowsJson)
    {
        if (string.IsNullOrWhiteSpace(entity)) return Task.FromResult(Refuse("Say which entity the rows are for."));
        if (mode is not ("append" or "overwrite" or "merge")) return Task.FromResult(Refuse("The mode is append, overwrite or merge."));
        JsonNode? rows;
        try { rows = JsonNode.Parse(rowsJson); } catch (JsonException) { return Task.FromResult(Refuse("The rows are not valid JSON.")); }
        if (rows is not JsonArray array || array.Count == 0 || array.Any(r => r is not JsonObject)) return Task.FromResult(Refuse("The rows are a JSON array of objects (field name to text), at least one."));

        var file = $"data/{entity.Trim()}.json";
        var files = new Dictionary<string, byte[]> { [file] = Encoding.UTF8.GetBytes(array.ToJsonString()) };
        return Task.FromResult(Build(PackageKind.Feed, null, toVersion, null, [], [new PackageDataSet(entity.Trim(), mode, file, array.Count)], files, $"pems-feed-{entity.Trim().ToLowerInvariant()}-{toVersion}.zip"));
    }

    public async Task<PackageFileResult> BuildInstallAsync(string toVersion, string schemaLevel, string? definitionRefs)
    {
        var (files, names, error) = await DefinitionFilesAsync(definitionRefs);
        return error != null ? Refuse(error) : Build(PackageKind.Install, null, toVersion, schemaLevel, names, [], files, $"pems-install-{toVersion}.zip");
    }

    public async Task<PackageFileResult> BuildUpdateAsync(string fromVersion, string toVersion, string schemaLevel, string? definitionRefs)
    {
        if (string.IsNullOrWhiteSpace(fromVersion)) return Refuse("An update names the version it comes from.");
        var (files, names, error) = await DefinitionFilesAsync(definitionRefs);
        return error != null ? Refuse(error) : Build(PackageKind.Update, fromVersion, toVersion, schemaLevel, names, [], files, $"pems-update-{fromVersion}-to-{toVersion}.zip");
    }

    private PackageFileResult Build(PackageKind kind, string? from, string to, string? schemaLevel, IReadOnlyList<string> definitionNames, IReadOnlyList<PackageDataSet> dataSets, Dictionary<string, byte[]> files, string fileName)
    {
        if (string.IsNullOrWhiteSpace(to)) return Refuse("Say which version the package goes to.");
        var keyText = configuration["Migrator:SigningKey"];
        var keyId = configuration["Migrator:SignerKeyId"];
        if (string.IsNullOrWhiteSpace(keyText) || string.IsNullOrWhiteSpace(keyId)) return Refuse("This installation has no vendor signing key; packages are built on the vendor's own deployment.");
        try
        {
            using var key = ECDsa.Create();
            key.ImportPkcs8PrivateKey(Convert.FromBase64String(keyText), out _);
            var bytes = MigrationPackageBuilder.Build(kind, from, to.Trim(), schemaLevel, definitionNames, dataSets, files, keyId, key);
            // verify before download: the same read the migrator center does, against the public key of this key
            var trust = new TrustedKeys(new Dictionary<string, string> { [keyId] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()) });
            MigrationPackage.Read(bytes, trust);
            log.LogInformation("Vendor package built: {File}, {Bytes} bytes", fileName, bytes.Length);
            return new PackageFileResult { Success = true, Bytes = bytes, FileName = fileName };
        }
        catch (Exception e) when (e is CryptographicException or FormatException or PackageRefusedException)
        {
            return Refuse("The package could not be signed or verified: " + e.Message);
        }
    }

    /// <summary>The published payload of each listed definition (appKey:kind:key) as a file of the package.</summary>
    private async Task<(Dictionary<string, byte[]> Files, List<string> Names, string? Error)> DefinitionFilesAsync(string? refs)
    {
        var files = new Dictionary<string, byte[]>();
        var names = new List<string>();
        foreach (var r in (refs ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = r.Split(':', 3);
            if (parts.Length != 3) return (new Dictionary<string, byte[]>(), new List<string>(), $"'{r}' is not appKey:kind:key.");
            var found = (await definitions.ListAsync(new DefinitionListQuery(parts[1], parts[0], parts[2], null, [], 1, 20))).Items.FirstOrDefault(d => d.Kind == parts[1] && d.Key == parts[2] && d.AppKey == parts[0]);
            if (found is null) return (new Dictionary<string, byte[]>(), new List<string>(), $"The definition {r} does not exist.");
            var export = await definitions.ExportAsync(found.Id, null);
            if (!export.Success || export.Value!.Items.Count == 0) return (new Dictionary<string, byte[]>(), new List<string>(), $"The definition {r} has no published version to ship.");
            files[$"definitions/{parts[0]}.{parts[1]}.{parts[2]}.json"] = JsonSerializer.SerializeToUtf8Bytes(export.Value.Items[0]);
            names.Add($"{parts[1]}/{parts[2]}");
        }
        return (files, names, null);
    }

    private static PackageFileResult Refuse(string why) => new() { Success = false, Error = why };
}
