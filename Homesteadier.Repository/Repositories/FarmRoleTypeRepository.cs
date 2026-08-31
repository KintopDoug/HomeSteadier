using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

[AutoRegister]
public class FarmRoleTypeRepository : Repository<FarmRoleType>, IFarmRoleTypeRepository
{
    public FarmRoleTypeRepository(HomesteadierDbContext context)
        : base(context)
    {
    }

    // Implement custom query methods here. Example:
    // public async Task<FarmRoleType?> GetByIdAsync(int id)
    // {
    //     return await _context.Set<FarmRoleType>()
    //         .FirstOrDefaultAsync(e => e.Id == id);
    // }
}