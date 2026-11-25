using HomeSteadier.Models.Database;
using HomeSteadier.Models.Request.Auth;
using Homesteadier.Services.Farms;
using Microsoft.AspNetCore.Identity;

namespace Homesteadier.Services.Auth;

/// <summary>
/// Account creation and session lifecycle. AuthController maps these results onto HTTP responses and
/// owns the refresh cookie; everything else about signing in lives here.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers an account (validating any invitation first, joining the farm after) and signs it in.
    /// </summary>
    Task<AuthResult<AuthSession>> SignUpAsync(RegisterRequest request);

    Task<AuthResult<AuthSession>> LoginAsync(LoginRequest request);

    /// <summary>
    /// Rotates a raw refresh token. Any failure — including a null token — is
    /// <see cref="AuthStatus.InvalidRefreshToken"/>.
    /// </summary>
    Task<AuthResult<AuthSession>> RefreshAsync(string? rawRefreshToken);

    /// <summary>Revokes the refresh token, if there is one and it is known.</summary>
    Task LogoutAsync(string? rawRefreshToken);

    Task<AuthResult<User>> GetCurrentUserAsync(int userId);
}

internal sealed class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly ISignUpInvitationService _farmInvitationService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IJwtTokenService _tokenService;
    private readonly ISessionService _sessionService;

    public AuthService(
        UserManager<User> userManager,
        ISignUpInvitationService farmInvitationService,
        IRefreshTokenService refreshTokenService,
        IJwtTokenService tokenService,
        ISessionService sessionService)
    {
        _userManager = userManager;
        _farmInvitationService = farmInvitationService;
        _refreshTokenService = refreshTokenService;
        _tokenService = tokenService;
        _sessionService = sessionService;
    }

    public async Task<AuthResult<AuthSession>> SignUpAsync(RegisterRequest request)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return AuthResult<AuthSession>.Failed(AuthStatus.EmailTaken);
        }

        // Validate the invitation up front, before creating the account, so a stale/invalid link
        // fails loudly rather than silently registering the user without joining them to a farm.
        FarmInvitation? invitation = null;
        if (!string.IsNullOrEmpty(request.InviteToken))
        {
            var inviteResult = await _farmInvitationService.ValidateForSignUpAsync(request.InviteToken, request.Email);

            switch (inviteResult.Status)
            {
                case SignUpInvitationStatus.InvalidOrExpired:
                    return AuthResult<AuthSession>.Failed(AuthStatus.InvalidInvitation);
                case SignUpInvitationStatus.EmailMismatch:
                    return AuthResult<AuthSession>.Failed(AuthStatus.InvitationEmailMismatch);
            }

            invitation = inviteResult.Value;
        }

        var user = new User
        {
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return AuthResult<AuthSession>.Failed(AuthStatus.IdentityFailed, result.Errors);
        }

        if (invitation is not null)
        {
            await _farmInvitationService.JoinAndConsumeAsync(user.Id, invitation);
        }

        return AuthResult<AuthSession>.Success(await _sessionService.IssueAsync(user));
    }

    public async Task<AuthResult<AuthSession>> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
        {
            return AuthResult<AuthSession>.Failed(AuthStatus.InvalidCredentials);
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return AuthResult<AuthSession>.Failed(AuthStatus.InvalidCredentials);
        }

        return AuthResult<AuthSession>.Success(await _sessionService.IssueAsync(user));
    }

    public async Task<AuthResult<AuthSession>> RefreshAsync(string? rawRefreshToken)
    {
        var rotated = rawRefreshToken is null ? null : await _refreshTokenService.RotateAsync(rawRefreshToken);
        if (rotated is null)
        {
            return AuthResult<AuthSession>.Failed(AuthStatus.InvalidRefreshToken);
        }

        var user = await _userManager.FindByIdAsync(rotated.Value.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            return AuthResult<AuthSession>.Failed(AuthStatus.InvalidRefreshToken);
        }

        var (accessToken, expiresAt) = _tokenService.CreateToken(user);
        return AuthResult<AuthSession>.Success(
            new AuthSession(user, accessToken, expiresAt, rotated.Value.NewRawToken, rotated.Value.ExpiresAt));
    }

    public async Task LogoutAsync(string? rawRefreshToken)
    {
        if (rawRefreshToken is not null)
        {
            await _refreshTokenService.RevokeAsync(rawRefreshToken);
        }
    }

    public async Task<AuthResult<User>> GetCurrentUserAsync(int userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user is null
            ? AuthResult<User>.Failed(AuthStatus.NotFound)
            : AuthResult<User>.Success(user);
    }
}
