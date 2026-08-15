using HomeSteadier.Models.Database;
using Homesteadier.Repository.Repositories;

namespace Homesteadier.API.Auth;

public interface IPasswordResetTokenService
{
    /// <summary>
    /// Issues a reset token for the user, first consuming any outstanding ones so only the newest
    /// emailed link works. Returns the raw value — it goes in the email link and nowhere else.
    /// </summary>
    Task<(string RawToken, DateTime ExpiresAt)> IssueAsync(int userId);

    /// <summary>
    /// Checks a raw token without consuming it. Returns the owning user id, or null if the token
    /// is unknown, expired, or already consumed — the caller must not distinguish the three.
    /// </summary>
    Task<int?> ValidateAsync(string rawToken);

    /// <summary>
    /// Consumes every outstanding token for the user. Called only after a password has actually
    /// changed — see the note on <see cref="ValidateAsync"/> for why the two are separate.
    /// </summary>
    Task ConsumeAllForUserAsync(int userId);
}

/// <summary>
/// Password-reset tokens, modelled on <see cref="RefreshTokenService"/>: a high-entropy opaque
/// value stored only as a hash, single-use, and short-lived.
///
/// Validation and consumption are deliberately separate operations. Consuming up front — the
/// obvious design — means a user whose new password fails the Identity validators has already
/// burned their only link and has to start the whole email round-trip again. So the controller
/// validates, sets the password, and only then consumes.
/// </summary>
public class PasswordResetTokenService : IPasswordResetTokenService
{
    private readonly IPasswordResetTokenRepository _repository;
    private readonly PasswordResetSettings _settings;

    public PasswordResetTokenService(IPasswordResetTokenRepository repository, PasswordResetSettings settings)
    {
        _repository = repository;
        _settings = settings;
    }

    public async Task<(string RawToken, DateTime ExpiresAt)> IssueAsync(int userId)
    {
        var now = DateTime.UtcNow;

        // Supersede outstanding links so a stolen older email can't be used after the real owner
        // requests a fresh one, and so the table doesn't accumulate live tokens per request.
        await _repository.ConsumeAllActiveForUserAsync(userId, now);

        var expiresAt = now.AddMinutes(_settings.TokenExpiryMinutes);
        var rawToken = SecureToken.Generate();

        await _repository.AddAsync(new PasswordResetToken
        {
            UserId = userId,
            TokenHash = SecureToken.Hash(rawToken),
            ExpiresAt = expiresAt,
        });
        await _repository.SaveChangesAsync();

        return (rawToken, expiresAt);
    }

    public async Task<int?> ValidateAsync(string rawToken)
    {
        if (string.IsNullOrEmpty(rawToken))
        {
            return null;
        }

        var existing = await _repository.GetByHashAsync(SecureToken.Hash(rawToken));

        if (existing is null || existing.ConsumedAt is not null || existing.ExpiresAt <= DateTime.UtcNow)
        {
            return null;
        }

        return existing.UserId;
    }

    public Task ConsumeAllForUserAsync(int userId)
        => _repository.ConsumeAllActiveForUserAsync(userId, DateTime.UtcNow);
}
