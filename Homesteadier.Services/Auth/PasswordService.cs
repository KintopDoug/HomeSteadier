using HomeSteadier.Models.Database;
using HomeSteadier.Models.Request.Auth;
using Homesteadier.Services.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Homesteadier.Services.Auth;

/// <summary>Password reset (emailed link) and password change (signed in).</summary>
public interface IPasswordService
{
    /// <summary>
    /// Emails a reset link when the address belongs to an active account. Returns nothing either way — a
    /// caller must not be able to learn whether an address has an account, and a send failure is swallowed
    /// (logged) for the same reason.
    /// </summary>
    Task RequestResetAsync(ForgotPasswordRequest request);

    /// <summary>
    /// Consumes an emailed reset token, sets the new password, and signs the user in. Order: validate the
    /// link, set the password, consume the token, revoke sessions, issue a new session — so a password the
    /// validators reject costs the user a retry rather than the whole email round-trip.
    /// </summary>
    Task<AuthResult<AuthSession>> ResetAsync(ResetPasswordRequest request);

    /// <summary>Changes the password of <paramref name="userId"/>, given their current one.</summary>
    Task<AuthResult<AuthSession>> ChangeAsync(int userId, ChangePasswordRequest request);
}

internal sealed class PasswordService : IPasswordService
{
    private readonly UserManager<User> _userManager;
    private readonly IPasswordResetTokenService _passwordResetTokenService;
    private readonly IPasswordUpdateService _passwordUpdateService;
    private readonly ISessionService _sessionService;
    private readonly IEmailSender _emailSender;
    private readonly PasswordResetSettings _passwordResetSettings;
    private readonly FrontendUrls _frontendUrls;
    private readonly ILogger<PasswordService> _logger;

    public PasswordService(
        UserManager<User> userManager,
        IPasswordResetTokenService passwordResetTokenService,
        IPasswordUpdateService passwordUpdateService,
        ISessionService sessionService,
        IEmailSender emailSender,
        PasswordResetSettings passwordResetSettings,
        FrontendUrls frontendUrls,
        ILogger<PasswordService> logger)
    {
        _userManager = userManager;
        _passwordResetTokenService = passwordResetTokenService;
        _passwordUpdateService = passwordUpdateService;
        _sessionService = sessionService;
        _emailSender = emailSender;
        _passwordResetSettings = passwordResetSettings;
        _frontendUrls = frontendUrls;
        _logger = logger;
    }

    public async Task RequestResetAsync(ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null || !user.IsActive)
        {
            // Logged, not returned. The caller's response is identical either way.
            _logger.LogInformation("Password reset requested for an unknown or inactive account.");
            return;
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
    }

    public async Task<AuthResult<AuthSession>> ResetAsync(ResetPasswordRequest request)
    {
        var userId = await _passwordResetTokenService.ValidateAsync(request.Token);
        if (userId is null)
        {
            return AuthResult<AuthSession>.Failed(AuthStatus.InvalidResetLink);
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null || !user.IsActive)
        {
            return AuthResult<AuthSession>.Failed(AuthStatus.InvalidResetLink);
        }

        // Note the ordering: the token is still unconsumed here, so a password the validators
        // reject costs the user a retry rather than the whole email round-trip.
        var result = await _passwordUpdateService.SetPasswordAsync(user, request.NewPassword);
        if (!result.Succeeded)
        {
            return AuthResult<AuthSession>.Failed(AuthStatus.IdentityFailed, result.Errors);
        }

        await _passwordResetTokenService.ConsumeAllForUserAsync(user.Id);

        return AuthResult<AuthSession>.Success(await _sessionService.RevokeAllAndIssueAsync(user));
    }

    public async Task<AuthResult<AuthSession>> ChangeAsync(int userId, ChangePasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return AuthResult<AuthSession>.Failed(AuthStatus.Unauthenticated);
        }

        // ChangePasswordAsync is safe against this UserStore (unlike ResetPasswordAsync, which
        // needs a security-stamp store): it verifies the current password and runs the validators,
        // and its security-stamp update is a no-op here rather than a throw.
        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
            {
                return AuthResult<AuthSession>.Failed(AuthStatus.IncorrectCurrentPassword);
            }

            return AuthResult<AuthSession>.Failed(AuthStatus.IdentityFailed, result.Errors);
        }

        // A reset link that was emailed before this deliberate change must not still work.
        await _passwordResetTokenService.ConsumeAllForUserAsync(user.Id);

        return AuthResult<AuthSession>.Success(await _sessionService.RevokeAllAndIssueAsync(user));
    }
}
