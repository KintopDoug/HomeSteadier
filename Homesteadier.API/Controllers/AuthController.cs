using Homesteadier.API.Auth;
using HomeSteadier.Models.Database;
using HomeSteadier.Models.Request.Auth;
using HomeSteadier.Models.Response.Auth;
using HomeSteadier.Models.Response.Users;
using Homesteadier.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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

    private readonly IAuthService _authService;
    private readonly IPasswordService _passwordService;
    private readonly RefreshCookieSettings _cookieSettings;

    public AuthController(
        IAuthService authService,
        IPasswordService passwordService,
        RefreshCookieSettings cookieSettings)
    {
        _authService = authService;
        _passwordService = passwordService;
        _cookieSettings = cookieSettings;
    }

    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    [HttpPost("SignUp")]
    public async Task<ActionResult<AuthResponse>> SignUp(RegisterRequest request)
    {
        var result = await _authService.SignUpAsync(request);

        return result.Status switch
        {
            AuthStatus.Success => CreatedAtAction(nameof(Me), null, SignIn(result.Value)),
            AuthStatus.EmailTaken => Conflict(new { message = "A user with that email already exists." }),
            AuthStatus.InvalidInvitation => BadRequest(new { message = "This invitation is invalid or has expired." }),
            AuthStatus.InvitationEmailMismatch => BadRequest(new { message = "This invitation was issued to a different email address." }),
            AuthStatus.IdentityFailed => BadRequest(IdentityFailure("Registration failed.", result)),
            AuthStatus.InvalidCredentials or AuthStatus.InvalidRefreshToken or AuthStatus.InvalidResetLink
                or AuthStatus.IncorrectCurrentPassword or AuthStatus.Unauthenticated or AuthStatus.NotFound
                => throw Unexpected(result),
        };
    }

    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    [HttpPost("Login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);

        return result.Status switch
        {
            AuthStatus.Success => Ok(SignIn(result.Value)),
            AuthStatus.InvalidCredentials => Unauthorized(new { message = "Invalid email or password." }),
            AuthStatus.EmailTaken or AuthStatus.InvalidInvitation or AuthStatus.InvitationEmailMismatch
                or AuthStatus.IdentityFailed or AuthStatus.InvalidRefreshToken or AuthStatus.InvalidResetLink
                or AuthStatus.IncorrectCurrentPassword or AuthStatus.Unauthenticated or AuthStatus.NotFound
                => throw Unexpected(result),
        };
    }

    [HttpPost("Refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh()
    {
        var result = await _authService.RefreshAsync(RefreshTokenCookie.Read(Request, _cookieSettings));

        if (!result.Succeeded)
        {
            // Every failure clears the cookie, so a client holding a dead token stops presenting it.
            RefreshTokenCookie.Clear(Response, _cookieSettings);
            return Unauthorized(new { message = "Invalid or expired refresh token." });
        }

        return Ok(SignIn(result.Value));
    }

    [HttpPost("Logout")]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync(RefreshTokenCookie.Read(Request, _cookieSettings));

        RefreshTokenCookie.Clear(Response, _cookieSettings);
        return NoContent();
    }

    /// <summary>
    /// Emails a password reset link. Always returns 202 — a caller must not be able to learn
    /// whether an address has an account here.
    /// </summary>
    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    [HttpPost("ForgotPassword")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await _passwordService.RequestResetAsync(request);
        return Accepted();
    }

    /// <summary>
    /// Consumes an emailed reset token, sets the new password, and signs the user in.
    /// </summary>
    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    [HttpPost("ResetPassword")]
    public async Task<ActionResult<AuthResponse>> ResetPassword(ResetPasswordRequest request)
    {
        var result = await _passwordService.ResetAsync(request);

        return result.Status switch
        {
            AuthStatus.Success => Ok(SignIn(result.Value)),
            AuthStatus.InvalidResetLink => BadRequest(new { message = InvalidResetLinkMessage }),
            AuthStatus.IdentityFailed => BadRequest(IdentityFailure("Password reset failed.", result)),
            AuthStatus.EmailTaken or AuthStatus.InvalidInvitation or AuthStatus.InvitationEmailMismatch
                or AuthStatus.InvalidCredentials or AuthStatus.InvalidRefreshToken
                or AuthStatus.IncorrectCurrentPassword or AuthStatus.Unauthenticated or AuthStatus.NotFound
                => throw Unexpected(result),
        };
    }

    /// <summary>
    /// Changes the password of the signed-in user, given their current one.
    /// </summary>
    [Authorize]
    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    [HttpPost("ChangePassword")]
    public async Task<ActionResult<AuthResponse>> ChangePassword(ChangePasswordRequest request)
    {
        var userId = User.UserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _passwordService.ChangeAsync(userId.Value, request);

        return result.Status switch
        {
            AuthStatus.Success => Ok(SignIn(result.Value)),
            AuthStatus.Unauthenticated => Unauthorized(),
            AuthStatus.IncorrectCurrentPassword => BadRequest(new { message = "Your current password is incorrect." }),
            AuthStatus.IdentityFailed => BadRequest(IdentityFailure("Password change failed.", result)),
            AuthStatus.EmailTaken or AuthStatus.InvalidInvitation or AuthStatus.InvitationEmailMismatch
                or AuthStatus.InvalidCredentials or AuthStatus.InvalidRefreshToken
                or AuthStatus.InvalidResetLink or AuthStatus.NotFound
                => throw Unexpected(result),
        };
    }

    [Authorize]
    [HttpGet("Me")]
    public async Task<ActionResult<UserResponse>> Me()
    {
        var userId = User.UserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _authService.GetCurrentUserAsync(userId.Value);

        return result.Status switch
        {
            AuthStatus.Success => Ok(UserResponse.FromEntity(result.Value)),
            AuthStatus.NotFound => NotFound(),
            AuthStatus.EmailTaken or AuthStatus.InvalidInvitation or AuthStatus.InvitationEmailMismatch
                or AuthStatus.IdentityFailed or AuthStatus.InvalidCredentials or AuthStatus.InvalidRefreshToken
                or AuthStatus.InvalidResetLink or AuthStatus.IncorrectCurrentPassword or AuthStatus.Unauthenticated
                => throw Unexpected(result),
        };
    }

    /// <summary>Hands the client its new refresh token (as the httpOnly cookie) and builds the response body.</summary>
    private AuthResponse SignIn(AuthSession session)
    {
        RefreshTokenCookie.Append(Response, session.RefreshToken, session.RefreshTokenExpiresAt, _cookieSettings);

        return new AuthResponse
        {
            Token = session.AccessToken,
            ExpiresAt = session.AccessTokenExpiresAt,
            User = UserResponse.FromEntity(session.User),
        };
    }

    private static object IdentityFailure(string message, AuthResult result) => new
    {
        message,
        errors = result.Errors.Select(e => new { e.Code, e.Description }),
    };

    private static InvalidOperationException Unexpected(AuthResult result)
        => new($"The auth service returned {result.Status}, which this action never produces.");
}
