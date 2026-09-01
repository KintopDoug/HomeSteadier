using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

public interface IFarmInvitationRepository : IRepository<FarmInvitation>
{
    /// <summary>Looks up an invitation by its stored SHA-256 digest (the raw token is never persisted).</summary>
    Task<FarmInvitation?> GetByHashAsync(string tokenHash);

    /// <summary>
    /// Marks every unaccepted, unexpired invitation for the (farm, email) pair as accepted. Used to
    /// supersede outstanding invitations when a new one is issued and to burn the used one after a
    /// successful accept — scoped to (farm, email) rather than a user id since the invitee may not
    /// have an account yet, and may hold live invitations to other farms at the same time.
    /// </summary>
    Task ConsumeAllActiveForFarmAndEmailAsync(int farmId, string email, DateTime when);
}