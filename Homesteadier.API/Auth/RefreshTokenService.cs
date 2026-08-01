using System.Security.Cryptography;
using System.Text;
using HomeSteadier.Models.Database;
using Homesteadier.Repository.Repositories;

namespace Homesteadier.API.Auth;

public interface IRefreshTokenService
{
    /// <summary>Issues a new refresh token for the user and returns the raw value (to put in the cookie).</summary>
    Task<(string RawToken, DateTime ExpiresAt)> IssueAsync(int userId);

    /// <summary>
    /// Validates and rotates a raw refresh token. On success returns the owning user id and a new
    /// raw token (the old one is revoked). Returns null if the token is missing, expired, or already
    /// revoked — a revoked hit also revokes the user's whole active token family (reuse detection).
    /// </summary>
    Task<(int UserId, string NewRawToken, DateTime ExpiresAt)?> RotateAsync(string rawToken);

    /// <summary>Revokes the token backing the given raw value, if any (logout).</summary>
    Task RevokeAsync(string rawToken);
}

public class RefreshTokenService : IRefreshTokenService
{
    private readonly IRefreshTokenRepository _repository;
    private readonly JwtSettings _settings;

    public RefreshTokenService(IRefreshTokenRepository repository, JwtSettings settings)
    {
        _repository = repository;
        _settings = settings;
    }

    public async Task<(string RawToken, DateTime ExpiresAt)> IssueAsync(int userId)
    {
        var expiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpiryDays);
        var rawToken = GenerateRawToken();

        await _repository.AddAsync(new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(rawToken),
            ExpiresAt = expiresAt,
        });
        await _repository.SaveChangesAsync();

        return (rawToken, expiresAt);
    }

    public async Task<(int UserId, string NewRawToken, DateTime ExpiresAt)?> RotateAsync(string rawToken)
    {
        if (string.IsNullOrEmpty(rawToken))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var existing = await _repository.GetByHashAsync(Hash(rawToken));

        if (existing is null || existing.ExpiresAt <= now)
        {
            return null;
        }

        // Presenting an already-revoked token means it was rotated away previously — treat as
        // theft and revoke every active token for the user.
        if (existing.RevokedAt is not null)
        {
            await _repository.RevokeAllActiveForUserAsync(existing.UserId, now);
            return null;
        }

        var newRaw = GenerateRawToken();
        var expiresAt = now.AddDays(_settings.RefreshTokenExpiryDays);

        var replacement = new RefreshToken
        {
            UserId = existing.UserId,
            TokenHash = Hash(newRaw),
            ExpiresAt = expiresAt,
        };
        await _repository.AddAsync(replacement);

        existing.RevokedAt = now;
        existing.ReplacedByHash = replacement.TokenHash;
        await _repository.UpdateAsync(existing);

        await _repository.SaveChangesAsync();

        return (existing.UserId, newRaw, expiresAt);
    }

    public async Task RevokeAsync(string rawToken)
    {
        if (string.IsNullOrEmpty(rawToken))
        {
            return;
        }

        var existing = await _repository.GetByHashAsync(Hash(rawToken));
        if (existing is null || existing.RevokedAt is not null)
        {
            return;
        }

        existing.RevokedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(existing);
        await _repository.SaveChangesAsync();
    }

    private static string GenerateRawToken()
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
