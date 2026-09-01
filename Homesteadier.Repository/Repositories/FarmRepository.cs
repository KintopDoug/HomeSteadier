using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

[AutoRegister]
public class FarmRepository : Repository<Farm>, IFarmRepository
{
    public FarmRepository(HomesteadierDbContext context)
        : base(context)
    {
    }

    // Implement custom query methods here. Example:
    // public async Task<Farm?> GetByIdAsync(int id)
    // {
    //     return await _context.Set<Farm>()
    //         .FirstOrDefaultAsync(e => e.Id == id);
    // }
}