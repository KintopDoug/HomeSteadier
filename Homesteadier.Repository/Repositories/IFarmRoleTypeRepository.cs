using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

public interface IFarmRoleTypeRepository : IRepository<FarmRoleType>
{
    // Example custom query - uncomment and modify for your needs:
    // Task<FarmRoleType?> GetByIdAsync(int id);
    //
    // Example filtered collection - uncomment and modify:
    // Task<List<FarmRoleType>> GetActiveAsync();
}