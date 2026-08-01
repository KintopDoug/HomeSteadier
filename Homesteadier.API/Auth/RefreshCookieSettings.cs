using Microsoft.AspNetCore.Http;

namespace Homesteadier.API.Auth;

/// <summary>
/// Configures the httpOnly refresh-token cookie. Bound from the "RefreshCookie" config section so
/// dev and prod can differ (e.g. SameSite=Lax locally vs None cross-site in prod).
/// </summary>
public class RefreshCookieSettings
{
    public string Name { get; set; } = "refreshToken";

    /// <summary>Scope the cookie to the auth routes so it isn't sent on every API call.</summary>
    public string Path { get; set; } = "/api/auth";

    /// <summary>One of "None", "Lax", "Strict". "None" requires Secure=true and HTTPS.</summary>
    public string SameSite { get; set; } = "None";

    public bool Secure { get; set; } = true;

    /// <summary>Optional cookie domain; leave null to scope to the exact host.</summary>
    public string? Domain { get; set; }

    public SameSiteMode SameSiteMode => SameSite.ToLowerInvariant() switch
    {
        "lax" => SameSiteMode.Lax,
        "strict" => SameSiteMode.Strict,
        _ => SameSiteMode.None,
    };
}
