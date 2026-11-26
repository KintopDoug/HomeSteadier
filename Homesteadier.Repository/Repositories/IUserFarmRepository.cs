using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

public interface IUserFarmRepository : IRepository<UserFarm>
{
    Task<List<UserFarm>> GetByUserIdAsync(int userId);

    /// <summary>Looks up a user's membership row on a specific farm, or null if they aren't a member.</summary>
    Task<UserFarm?> GetByUserAndFarmAsync(int userId, int farmId);

    /// <summary>Every membership on a farm, with the member's user row and role loaded.</summary>
    Task<List<UserFarm>> GetByFarmIdAsync(int farmId);

    /// <summary>Number of memberships on the farm holding the given role.</summary>
    Task<int> CountByFarmAndRoleAsync(int farmId, int farmRoleTypeId);
}
