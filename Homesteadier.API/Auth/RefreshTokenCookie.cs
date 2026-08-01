using Microsoft.AspNetCore.Http;

namespace Homesteadier.API.Auth;

/// <summary>Centralizes reading/writing/clearing the refresh-token cookie with consistent options.</summary>
public static class RefreshTokenCookie
{
    public static string? Read(HttpRequest request, RefreshCookieSettings settings)
        => request.Cookies.TryGetValue(settings.Name, out var value) ? value : null;

    public static void Append(HttpResponse response, string rawToken, DateTime expiresUtc, RefreshCookieSettings settings)
    {
        response.Cookies.Append(settings.Name, rawToken, BuildOptions(settings, expiresUtc));
    }

    public static void Clear(HttpResponse response, RefreshCookieSettings settings)
    {
        // Expire in the past so the browser drops it. Options (Path/SameSite/Secure/Domain) must
        // match the ones used when setting it, or the browser won't overwrite the cookie.
        response.Cookies.Append(settings.Name, string.Empty, BuildOptions(settings, DateTime.UtcNow.AddDays(-1)));
    }

    private static CookieOptions BuildOptions(RefreshCookieSettings settings, DateTime expiresUtc) => new()
    {
        HttpOnly = true,
        Secure = settings.Secure,
        SameSite = settings.SameSiteMode,
        Path = settings.Path,
        Domain = settings.Domain,
        Expires = new DateTimeOffset(expiresUtc, TimeSpan.Zero),
        IsEssential = true,
    };
}
