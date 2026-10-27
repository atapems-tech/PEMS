namespace PEMS.Web.Licensing;

/// <summary>
/// The one allowlist of surfaces that never need a licence (plan rows #110, merged #111; Instructions rule 11).
/// Everything not matched here is checked. Add an entry only with the owner's agreement; the public demo page of the
/// website is a separate deployable and is not part of this product.
/// </summary>
public static class LicenceFreeSurfaces
{
    private static readonly string[] Prefixes =
    {
        "/Account",          // login, logout, passkeys: an administrator must always be able to sign in and fix the licence
        "/Admin/License",    // the licence page itself
        "/QuickSetup",       // first-run setup
        "/Culture",          // language switch
        "/Home/Error", "/Home/StatusPage",
        "/health",           // details-free probes used by deploy pipelines (open question Q9 of the design)
    };

    public static bool IsFree(string? path)
    {
        if (string.IsNullOrEmpty(path) || path == "/") return false;
        foreach (var p in Prefixes)
            if (path.Equals(p, StringComparison.OrdinalIgnoreCase) || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
