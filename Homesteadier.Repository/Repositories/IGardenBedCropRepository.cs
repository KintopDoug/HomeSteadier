using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

public interface IGardenBedCropRepository : IRepository<GardenBedCrop>
{
    // Example custom query - uncomment and modify for your needs:
    // Task<GardenBedCrop?> GetByIdAsync(int id);
    //
    // Example filtered collection - uncomment and modify:
    // Task<List<GardenBedCrop>> GetActiveAsync();
}