using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

[AutoRegister]
public class UserFarmRepository : Repository<UserFarm>, IUserFarmRepository
{
    public UserFarmRepository(HomesteadierDbContext context)
        : base(context)
    {
    }

    // Implement custom query methods here. Example:
    // public async Task<UserFarm?> GetByIdAsync(int id)
    // {
    //     return await _context.Set<UserFarm>()
    //         .FirstOrDefaultAsync(e => e.Id == id);
    // }
}