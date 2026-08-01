using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash);

    /// <summary>Revokes every not-yet-revoked, unexpired token for the user (reuse/theft response).</summary>
    Task RevokeAllActiveForUserAsync(int userId, DateTime when);
}
