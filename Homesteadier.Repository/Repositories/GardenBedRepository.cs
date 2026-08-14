using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

[AutoRegister]
public class GardenBedRepository : Repository<GardenBed>, IGardenBedRepository
{
    public GardenBedRepository(HomesteadierDbContext context)
        : base(context)
    {
    }

    // Implement custom query methods here. Example:
    // public async Task<GardenBed?> GetByIdAsync(int id)
    // {
    //     return await _context.Set<GardenBed>()
    //         .FirstOrDefaultAsync(e => e.Id == id);
    // }
}