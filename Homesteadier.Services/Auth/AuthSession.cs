using HomeSteadier.Models.Database;

namespace Homesteadier.Services.Auth;

/// <summary>
/// A freshly-issued session: the user, a short-lived access token, and the raw refresh token the caller
/// must hand to the client (the API puts it in an httpOnly cookie). The raw refresh token exists nowhere
/// else — only its hash is stored.
/// </summary>
public sealed record AuthSession(
    User User,
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);
