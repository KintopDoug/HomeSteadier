using Homesteadier.API.Auth;
using Homesteadier.API.Email;
using Homesteadier.API.Farms;
using HomeSteadier.Models.Database;
using HomeSteadier.Models.Request.Auth;
using HomeSteadier.Models.Response.Auth;
using HomeSteadier.Models.Response.Users;
using Homesteadier.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Homesteadier.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    /// <summary>
    /// Every failure mode of a reset link — unknown, expired, already used, superseded, or
    /// belonging to a deactivated account — reports this single message. Distinguishing them
    /// would tell an attacker which tokens exist.
    /// </summary>
    private const string InvalidResetLinkMessage =
        "This password reset link is invalid or has expired. Please request a new one.";

    private readonly UserManager<User> _userManager;
    private readonly IJwtTokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordResetTokenService _passwordResetTokenService;
    private readonly IPasswordUpdateService _passwordUpdateService;
    private readonly IFarmInvitationTokenService _farmInvitationTokenService;
    private readonly IUserFarmRepository _userFarmRepository;
    private readonly IEmailSender _emailSender;
    private readonly PasswordResetSettings _passwordResetSettings;
    private readonly FrontendUrls _frontendUrls;
    private readonly RefreshCookieSettings _cookieSettings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<User> userManager,
        IJwtTokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordResetTokenService passwordResetTokenService,
        IPasswordUpdateService passwordUpdateService,
        IFarmInvitationTokenService farmInvitationTokenService,
        IUserFarmRepository userFarmRepository,
        IEmailSender emailSender,
        PasswordResetSettings passwordResetSettings,
        FrontendUrls frontendUrls,
        RefreshCookieSettings cookieSettings,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordResetTokenService = passwordResetTokenService;
        _passwordUpdateService = passwordUpdateService;
        _farmInvitationTokenService = farmInvitationTokenService;
        _userFarmRepository = userFarmRepository;
        _emailSender = emailSender;
        _passwordResetSettings = passwordResetSettings;
        _frontendUrls = frontendUrls;
        _cookieSettings = cookieSettings;
        _logger = logger;
    }

    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    [HttpPost("SignUp")]
    public async Task<ActionResult<AuthResponse>> SignUp(RegisterRequest request)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return Conflict(new { message = "A user with that email already exists." });
        }

        // Validate the invitation up front, before creating the account, so a stale/invalid link
        // fails loudly rather than silently registering the user without joining them to a farm.
        FarmInvitation? invitation = null;
        if (!string.IsNullOrEmpty(request.InviteToken))
        {
            invitation = await _farmInvitationTokenService.ValidateAsync(request.InviteToken);
            if (invitation is null)
            {
                return BadRequest(new { message = "This invitation is invalid or has expired." });
            }

            if (!string.Equals(invitation.Email, request.Email, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "This invitation was issued to a different email address." });
            }
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
            return BadRequest(new
            {
                message = "Registration failed.",
                errors = result.Errors.Select(e => new { e.Code, e.Description }),
            });
        }

        if (invitation is not null)
        {
            await _userFarmRepository.AddAsync(new UserFarm
            {
                UserId = user.Id,
                FarmId = invitation.FarmId,
                FarmRoleTypeId = invitation.FarmRoleTypeId,
            });
            await _userFarmRepository.SaveChangesAsync();

            await _farmInvitationTokenService.ConsumeAsync(invitation);
        }

        var response = await IssueTokensAsync(user);
        return CreatedAtAction(nameof(Me), null, response);
    }

    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh()
    {
        var rawToken = RefreshTokenCookie.Read(Request, _cookieSettings);
        var rotated = rawToken is null ? null : await _refreshTokenService.RotateAsync(rawToken);

        if (rotated is null)
        {
            RefreshTokenCookie.Clear(Response, _cookieSettings);
            return Unauthorized(new { message = "Invalid or expired refresh token." });
        }

        var user = await _userManager.FindByIdAsync(rotated.Value.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            RefreshTokenCookie.Clear(Response, _cookieSettings);
            return Unauthorized(new { message = "Invalid or expired refresh token." });
        }

        RefreshTokenCookie.Append(Response, rotated.Value.NewRawToken, rotated.Value.ExpiresAt, _cookieSettings);

        var (accessToken, expiresAt) = _tokenService.CreateToken(user);
        return Ok(new AuthResponse
        {
            Token = accessToken,
            ExpiresAt = expiresAt,
            User = UserResponse.FromEntity(user),
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var rawToken = RefreshTokenCookie.Read(Request, _cookieSettings);
        if (rawToken is not null)
        {
            await _refreshTokenService.RevokeAsync(rawToken);
        }

        RefreshTokenCookie.Clear(Response, _cookieSettings);
        return NoContent();
    }

    /// <summary>
    /// Emails a password reset link. Always returns 202 — a caller must not be able to learn
    /// whether an address has an account here.
    /// </summary>
    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    [HttpPost("forgotPassword")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null || !user.IsActive)
        {
            // Logged, not returned. The response below is identical either way.
            _logger.LogInformation("Password reset requested for an unknown or inactive account.");
            return Accepted();
        }

        var (rawToken, _) = await _passwordResetTokenService.IssueAsync(user.Id);

        var (subject, htmlBody, plainTextBody) = PasswordResetEmail.Compose(
            user.FirstName,
            _frontendUrls.PasswordResetLink(rawToken),
            _passwordResetSettings.TokenExpiryMinutes);

        try
        {
            await _emailSender.SendAsync(user.Email, subject, htmlBody, plainTextBody);
        }
        catch (Exception ex)
        {
            // A send failure must not change the response either — same reasoning as above.
            _logger.LogError(ex, "Failed to send a password reset email for user {UserId}.", user.Id);
        }

        return Accepted();
    }

    /// <summary>
    /// Consumes an emailed reset token, sets the new password, and signs the user in.
    /// </summary>
    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    [HttpPost("resetPassword")]
    public async Task<ActionResult<AuthResponse>> ResetPassword(ResetPasswordRequest request)
    {
        var userId = await _passwordResetTokenService.ValidateAsync(request.Token);
        if (userId is null)
        {
            return BadRequest(new { message = InvalidResetLinkMessage });
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null || !user.IsActive)
        {
            return BadRequest(new { message = InvalidResetLinkMessage });
        }

        // Note the ordering: the token is still unconsumed here, so a password the validators
        // reject costs the user a retry rather than the whole email round-trip.
        var result = await _passwordUpdateService.SetPasswordAsync(user, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Password reset failed.",
                errors = result.Errors.Select(e => new { e.Code, e.Description }),
            });
        }

        await _passwordResetTokenService.ConsumeAllForUserAsync(user.Id);

        return Ok(await ResetSessionsAndSignInAsync(user));
    }

    /// <summary>
    /// Changes the password of the signed-in user, given their current one.
    /// </summary>
    [Authorize]
    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    [HttpPost("changePassword")]
    public async Task<ActionResult<AuthResponse>> ChangePassword(ChangePasswordRequest request)
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        // ChangePasswordAsync is safe against this UserStore (unlike ResetPasswordAsync, which
        // needs a security-stamp store): it verifies the current password and runs the validators,
        // and its security-stamp update is a no-op here rather than a throw.
        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
            {
                return BadRequest(new { message = "Your current password is incorrect." });
            }

            return BadRequest(new
            {
                message = "Password change failed.",
                errors = result.Errors.Select(e => new { e.Code, e.Description }),
            });
        }

        // A reset link that was emailed before this deliberate change must not still work.
        await _passwordResetTokenService.ConsumeAllForUserAsync(user.Id);

        return Ok(await ResetSessionsAndSignInAsync(user));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me()
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(UserResponse.FromEntity(user));
    }

    /// <summary>
    /// Logs every other device out and starts a fresh session for the caller. Used after a
    /// password change, where the point is that sessions established with the old password stop
    /// working — the caller keeps theirs only because it's reissued here.
    ///
    /// The order is load-bearing: revoking runs first, so the token issued below survives it.
    /// (RevokeAllActiveForUserAsync issues its UPDATE immediately and bypasses the change
    /// tracker, so it doesn't interact with the insert inside IssueTokensAsync.)
    ///
    /// Access tokens are not revocable — there's no security stamp in the JWT — so other devices
    /// stay usable until their current one expires (Jwt:ExpiryMinutes, 15 by default).
    /// </summary>
    private async Task<AuthResponse> ResetSessionsAndSignInAsync(User user)
    {
        await _refreshTokenRepository.RevokeAllActiveForUserAsync(user.Id, DateTime.UtcNow);
        return await IssueTokensAsync(user);
    }

    /// <summary>Issues an access token + a rotating refresh cookie for a freshly-authenticated user.</summary>
    private async Task<AuthResponse> IssueTokensAsync(User user)
    {
        var (accessToken, expiresAt) = _tokenService.CreateToken(user);
        var (rawRefreshToken, refreshExpiresAt) = await _refreshTokenService.IssueAsync(user.Id);

        RefreshTokenCookie.Append(Response, rawRefreshToken, refreshExpiresAt, _cookieSettings);

        return new AuthResponse
        {
            Token = accessToken,
            ExpiresAt = expiresAt,
            User = UserResponse.FromEntity(user),
        };
    }
}
