using HomeSteadier.Models.Database;
using Homesteadier.Repository.Repositories;

namespace Homesteadier.Services.Auth;

/// <summary>Issues sessions (access token + rotating refresh token) and revokes them.</summary>
internal interface ISessionService
{
    /// <summary>Issues an access token and a refresh token for a freshly-authenticated user.</summary>
    Task<AuthSession> IssueAsync(User user);

    /// <summary>
    /// Logs every other device out and starts a fresh session for the caller. Used after a
    /// password change, where the point is that sessions established with the old password stop
    /// working — the caller keeps theirs only because it's reissued here.
    ///
    /// The order is load-bearing: revoking runs first, so the token issued afterwards survives it.
    /// (RevokeAllActiveForUserAsync issues its UPDATE immediately and bypasses the change
    /// tracker, so it doesn't interact with the insert inside <see cref="IssueAsync"/>.)
    ///
    /// Access tokens are not revocable — there's no security stamp in the JWT — so other devices
    /// stay usable until their current one expires (Jwt:ExpiryMinutes, 15 by default).
    /// </summary>
    Task<AuthSession> RevokeAllAndIssueAsync(User user);
}

internal sealed class SessionService : ISessionService
{
    private readonly IJwtTokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public SessionService(
        IJwtTokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IRefreshTokenRepository refreshTokenRepository)
    {
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<AuthSession> IssueAsync(User user)
    {
        var (accessToken, expiresAt) = _tokenService.CreateToken(user);
        var (rawRefreshToken, refreshExpiresAt) = await _refreshTokenService.IssueAsync(user.Id);

        return new AuthSession(user, accessToken, expiresAt, rawRefreshToken, refreshExpiresAt);
    }

    public async Task<AuthSession> RevokeAllAndIssueAsync(User user)
    {
        await _refreshTokenRepository.RevokeAllActiveForUserAsync(user.Id, DateTime.UtcNow);
        return await IssueAsync(user);
    }
}
