using HomeSteadier.Models.Database;
using Homesteadier.Repository;
using Microsoft.EntityFrameworkCore;

namespace Homesteadier.Repository.Repositories;

[AutoRegister]
public class FarmRoleTypeRepository : Repository<FarmRoleType>, IFarmRoleTypeRepository
{
    public FarmRoleTypeRepository(HomesteadierDbContext context)
        : base(context)
    {
    }

    public async Task<FarmRoleType?> GetByNameAsync(string name)
    {
        return await _context.Set<FarmRoleType>()
            .FirstOrDefaultAsync(r => r.Name == name);
    }
}