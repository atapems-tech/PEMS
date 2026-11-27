using System.Globalization;
using PEMS.Application.Definitions;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using PEMS.Web.Definitions;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// The file host (6b of the definition-only sprint): <c>GET /x/{key}</c> answers a file or JSON from the data source definition {key} (provider <c>operation-file</c>): the query values are the arguments the definition names
/// as <c>param:x</c>. The operation answers a file when its result has Bytes (with FileName and ContentType), otherwise its result as JSON. The definition and the operation decide who may; a user without the right gets 403,
/// an unknown key or a missing argument 404. Nothing is cached by the browser or a proxy.
/// </summary>
[Route("x/{key}")]
public sealed class FileHostController(IDefinitionStore store, IDefinitionAccess access, OperationReader reader, ICurrentUserService currentUser) : Controller
{
    private static readonly Regex KeyShape = new("^[a-z0-9][a-z0-9._-]{0,80}$", RegexOptions.Compiled);

    [HttpGet("")]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
    {
        if (!KeyShape.IsMatch(key)) return NotFound();
        var roles = User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
        var published = await store.GetPublishedAsync("data-source", key, "core", ct);
        if (published is null || JsonNode.Parse(published.PayloadJson) is not JsonObject root || DataSourceDefinitionKind.Str(root["provider"]) is not ("operation-file" or "pdf")) return NotFound();
        if (!await access.CanOpenAsync("data-source", key, DataSourceDefinitionKind.Str(root["permission"]), roles, ct)) return Forbid();

        if (DataSourceDefinitionKind.Str(root["provider"]) == "pdf") return await PdfAsync(root, roles, ct);
        var parameters = (root["parameters"] as JsonObject)?.ToDictionary(p => p.Key, p => (string?)DataSourceDefinitionKind.Str(p.Value) ?? p.Value?.ToJsonString(), StringComparer.Ordinal) ?? new Dictionary<string, string?>();
        var query = Request.Query.ToDictionary(q => q.Key, q => (string?)q.Value.ToString(), StringComparer.Ordinal);
        OperationReader.Invoked invoked;
        try { invoked = await reader.InvokeAsync(parameters, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, roles, currentUser.UserName, ct, query); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        if (!invoked.Allowed) return Forbid();
        if (invoked.NoInput || invoked.Answer is null) return NotFound();

        Response.Headers.CacheControl = "no-store";
        var type = invoked.Answer.GetType();
        if (type.GetProperty("RedirectUrl")?.GetValue(invoked.Answer) is string redirectUrl)      // SCRUM-841: an answer that leads on: only a path of this site, a refusal is a 404
            return type.GetProperty("Success")?.GetValue(invoked.Answer) is false || !Url.IsLocalUrl(redirectUrl) ? NotFound() : LocalRedirect(redirectUrl);
        if (type.GetProperty("Bytes")?.GetValue(invoked.Answer) is byte[] bytes)
        {
            if (type.GetProperty("Success")?.GetValue(invoked.Answer) is false) return NotFound();
            var contentType = type.GetProperty("ContentType")?.GetValue(invoked.Answer) as string ?? "application/octet-stream";
            var fileName = type.GetProperty("FileName")?.GetValue(invoked.Answer) as string;
            return parameters.GetValueOrDefault("disposition") == "inline" || fileName is null ? File(bytes, contentType) : File(bytes, contentType, fileName);
        }
        return Json(invoked.Answer);
    }

    /// <summary>SCRUM-839: a PDF from a layout definition: every dataset is a read operation run with the rights of the user (one that is refused refuses the file), then the one renderer draws the layout.</summary>
    private async Task<IActionResult> PdfAsync(JsonObject root, List<string> roles, CancellationToken ct)
    {
        var parameters = root["parameters"] as JsonObject ?? [];
        var query = Request.Query.ToDictionary(q => q.Key, q => (string?)q.Value.ToString(), StringComparer.Ordinal);
        var culture = CultureInfo.CurrentUICulture;
        var data = new Dictionary<string, JsonArray>(StringComparer.Ordinal);
        foreach (var (name, spec) in parameters["datasets"] as JsonObject ?? [])
        {
            if (spec is not JsonObject op) continue;
            var call = new Dictionary<string, string?> { ["model"] = DataSourceDefinitionKind.Str(op["model"]), ["action"] = DataSourceDefinitionKind.Str(op["action"]), ["args"] = (op["args"] as JsonObject)?.ToJsonString() };
            OperationReader.Invoked invoked;
            try { invoked = await reader.InvokeAsync(call, culture.TwoLetterISOLanguageName, roles, currentUser.UserName, ct, query); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            if (!invoked.Allowed) return Forbid();
            if (invoked.NoInput || invoked.Answer is null) return NotFound();
            var node = System.Text.Json.JsonSerializer.SerializeToNode(invoked.Answer, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            data[name] = node is JsonArray array ? array : node is JsonObject one ? new JsonArray(one.DeepClone()) : [];
        }
        if (parameters["layout"] is not JsonObject layout) return NotFound();
        var bytes = PdfLayoutRenderer.Render(layout, data, culture);
        Response.Headers.CacheControl = "no-store";
        var fileName = DataSourceDefinitionKind.Str(parameters["fileName"]) is { } text ? System.Text.RegularExpressions.Regex.Replace(text, @"\{\{([A-Za-z0-9_.]+)\}\}", m => ValueOf(data, m.Groups[1].Value)) : null;
        fileName = string.IsNullOrWhiteSpace(fileName) ? "document" : System.Text.RegularExpressions.Regex.Replace(fileName, @"[^\w.\- ]", "_");
        return DataSourceDefinitionKind.Str(parameters["disposition"]) == "inline" ? File(bytes, "application/pdf") : File(bytes, "application/pdf", fileName + ".pdf");
    }

    private static string ValueOf(Dictionary<string, JsonArray> data, string path)
    {
        var dot = path.IndexOf('.');
        if (dot <= 0 || !data.TryGetValue(path[..dot], out var rows) || rows.Count == 0 || rows[0] is not JsonObject first) return "";
        JsonNode? cur = first;
        foreach (var part in path[(dot + 1)..].Split('.')) { cur = (cur as JsonObject)?[part]; if (cur is null) return ""; }
        return cur.ToString();
    }
}
