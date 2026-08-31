using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

public interface IUserFarmRepository : IRepository<UserFarm>
{
    // Example custom query - uncomment and modify for your needs:
    // Task<UserFarm?> GetByIdAsync(int id);
    //
    // Example filtered collection - uncomment and modify:
    // Task<List<UserFarm>> GetActiveAsync();
}