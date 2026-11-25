using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

public interface IUserFarmRepository : IRepository<UserFarm>
{
    Task<List<UserFarm>> GetByUserIdAsync(int userId);

    /// <summary>Looks up a user's membership row on a specific farm, or null if they aren't a member.</summary>
    Task<UserFarm?> GetByUserAndFarmAsync(int userId, int farmId);

    /// <summary>All memberships of a farm, with user and role loaded, ordered by name.</summary>
    Task<List<UserFarm>> GetByFarmIdAsync(int farmId);

    /// <summary>Like <see cref="GetByUserAndFarmAsync"/> but change-tracked, so edits save.</summary>
    Task<UserFarm?> GetTrackedByUserAndFarmAsync(int userId, int farmId);

    /// <summary>How many members of the farm hold the given role.</summary>
    Task<int> CountByFarmAndRoleAsync(int farmId, int farmRoleTypeId);
}
