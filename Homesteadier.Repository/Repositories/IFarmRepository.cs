using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

public interface IFarmRepository : IRepository<Farm>
{
    // Example custom query - uncomment and modify for your needs:
    // Task<Farm?> GetByIdAsync(int id);
    //
    // Example filtered collection - uncomment and modify:
    // Task<List<Farm>> GetActiveAsync();
}