using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

public interface IGardenBedRepository : IRepository<GardenBed>
{
    // Example custom query - uncomment and modify for your needs:
    // Task<GardenBed?> GetByIdAsync(int id);
    //
    // Example filtered collection - uncomment and modify:
    // Task<List<GardenBed>> GetActiveAsync();
}