using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

public interface ICropTypeRepository : IRepository<CropType>
{
    // Example custom query - uncomment and modify for your needs:
    // Task<CropType?> GetByIdAsync(int id);
    //
    // Example filtered collection - uncomment and modify:
    // Task<List<CropType>> GetActiveAsync();
}