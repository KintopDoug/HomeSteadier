using Homesteadier.API.Auth;
using HomeSteadier.Models.Database;
using Homesteadier.Repository.Repositories;

namespace Homesteadier.API.Farms;

public interface IFarmInvitationTokenService
{
    /// <summary>
    /// Issues an invitation token for the (farm, email) pair, first consuming any outstanding ones
    /// so only the newest emailed link works. Returns the raw value — it goes in the email link and
    /// nowhere else.
    /// </summary>
    Task<(string RawToken, DateTime ExpiresAt)> IssueAsync(int farmId, string email, int farmRoleTypeId, int invitedByUserId);

    /// <summary>
    /// Checks a raw token without consuming it. Returns the invitation (with Farm/FarmRoleType
    /// loaded), or null if the token is unknown, expired, or already accepted — the caller must not
    /// distinguish the three.
    /// </summary>
    Task<FarmInvitation?> ValidateAsync(string rawToken);

    /// <summary>
    /// Marks a single invitation as accepted. Called only after the invitee has actually been added
    /// to the farm — see the note on <see cref="ValidateAsync"/> for why the two are separate.
    /// </summary>
    Task ConsumeAsync(FarmInvitation invitation);
}

/// <summary>
/// Farm invitation tokens, modelled on <see cref="PasswordResetTokenService"/>: a high-entropy
/// opaque value stored only as a hash, single-use, and time-limited.
/// </summary>
public class FarmInvitationTokenService : IFarmInvitationTokenService
{
    private readonly IFarmInvitationRepository _repository;
    private readonly FarmInvitationSettings _settings;

    public FarmInvitationTokenService(IFarmInvitationRepository repository, FarmInvitationSettings settings)
    {
        _repository = repository;
        _settings = settings;
    }

    public async Task<(string RawToken, DateTime ExpiresAt)> IssueAsync(int farmId, string email, int farmRoleTypeId, int invitedByUserId)
    {
        var now = DateTime.UtcNow;

        await _repository.ConsumeAllActiveForFarmAndEmailAsync(farmId, email, now);

        var expiresAt = now.AddDays(_settings.TokenExpiryDays);
        var rawToken = SecureToken.Generate();

        await _repository.AddAsync(new FarmInvitation
        {
            FarmId = farmId,
            Email = email,
            FarmRoleTypeId = farmRoleTypeId,
            InvitedByUserId = invitedByUserId,
            TokenHash = SecureToken.Hash(rawToken),
            ExpiresAt = expiresAt,
        });
        await _repository.SaveChangesAsync();

        return (rawToken, expiresAt);
    }

    public async Task<FarmInvitation?> ValidateAsync(string rawToken)
    {
        if (string.IsNullOrEmpty(rawToken))
        {
            return null;
        }

        var existing = await _repository.GetByHashAsync(SecureToken.Hash(rawToken));

        if (existing is null || existing.AcceptedAt is not null || existing.ExpiresAt <= DateTime.UtcNow)
        {
            return null;
        }

        return existing;
    }

    public async Task ConsumeAsync(FarmInvitation invitation)
    {
        invitation.AcceptedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(invitation);
        await _repository.SaveChangesAsync();
    }
}
