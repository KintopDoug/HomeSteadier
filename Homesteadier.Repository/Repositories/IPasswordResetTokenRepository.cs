using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

public interface IPasswordResetTokenRepository : IRepository<PasswordResetToken>
{
    /// <summary>Looks up a token by its stored SHA-256 digest (the raw value is never persisted).</summary>
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash);

    /// <summary>
    /// Marks every unconsumed, unexpired token for the user as consumed. Used both to supersede
    /// outstanding links when a new one is issued and to burn the used link after a successful
    /// password change.
    /// </summary>
    Task ConsumeAllActiveForUserAsync(int userId, DateTime when);
}
