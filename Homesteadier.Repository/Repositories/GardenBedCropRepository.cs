using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

[AutoRegister]
public class GardenBedCropRepository : Repository<GardenBedCrop>, IGardenBedCropRepository
{
    public GardenBedCropRepository(HomesteadierDbContext context)
        : base(context)
    {
    }

    // Implement custom query methods here. Example:
    // public async Task<GardenBedCrop?> GetByIdAsync(int id)
    // {
    //     return await _context.Set<GardenBedCrop>()
    //         .FirstOrDefaultAsync(e => e.Id == id);
    // }
}