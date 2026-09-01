namespace Homesteadier.Services;

/// <summary>
/// The SPA's origin, used to build links the API emails to users (currently just password
/// reset). Resolved once at startup — see ResolveFrontendBaseUrl in Program.cs.
/// </summary>
/// <param name="BaseUrl">Absolute http(s) origin, no trailing slash.</param>
public record FrontendUrls(string BaseUrl)
{
    public string PasswordResetLink(string rawToken)
        => $"{BaseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";

    public string AcceptFarmInvitationLink(string rawToken)
        => $"{BaseUrl}/accept-invitation?token={Uri.EscapeDataString(rawToken)}";

    public string RegisterWithInviteLink(string rawToken)
        => $"{BaseUrl}/register?inviteToken={Uri.EscapeDataString(rawToken)}";
}
