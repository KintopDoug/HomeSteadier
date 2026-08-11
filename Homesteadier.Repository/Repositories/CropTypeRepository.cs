using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

[AutoRegister]
public class CropTypeRepository : Repository<CropType>, ICropTypeRepository
{
    public CropTypeRepository(HomesteadierDbContext context)
        : base(context)
    {
    }

    // Implement custom query methods here. Example:
    // public async Task<CropType?> GetByIdAsync(int id)
    // {
    //     return await _context.Set<CropType>()
    //         .FirstOrDefaultAsync(e => e.Id == id);
    // }
}