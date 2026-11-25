using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

public interface IUserFarmRepository : IRepository<UserFarm>
{
    Task<List<UserFarm>> GetByUserIdAsync(int userId);

    /// <summary>Looks up a user's membership row on a specific farm, or null if they aren't a member.</summary>
    Task<UserFarm?> GetByUserAndFarmAsync(int userId, int farmId);

    /// <summary>Every membership on a farm, with user and role loaded, ordered by name.</summary>
    Task<List<UserFarm>> GetByFarmIdAsync(int farmId);

    /// <summary>Looks up a membership as a tracked entity (user and role loaded) so it can be updated or removed.</summary>
    Task<UserFarm?> GetTrackedByUserAndFarmAsync(int userId, int farmId);

    /// <summary>Number of members on a farm holding the given role.</summary>
    Task<int> CountByFarmAndRoleAsync(int farmId, int farmRoleTypeId);
}