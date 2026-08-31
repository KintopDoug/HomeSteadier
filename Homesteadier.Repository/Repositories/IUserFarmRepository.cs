using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

public interface IUserFarmRepository : IRepository<UserFarm>
{
    Task<List<UserFarm>> GetByUserIdAsync(int userId);

    /// <summary>Looks up a user's membership row on a specific farm, or null if they aren't a member.</summary>
    Task<UserFarm?> GetByUserAndFarmAsync(int userId, int farmId);
}