using HomeSteadier.Models.Database;
using Homesteadier.Repository;
using Microsoft.EntityFrameworkCore;

namespace Homesteadier.Repository.Repositories;

[AutoRegister]
public class UserFarmRepository : Repository<UserFarm>, IUserFarmRepository
{
    public UserFarmRepository(HomesteadierDbContext context)
        : base(context)
    {
    }

    public async Task<List<UserFarm>> GetByUserIdAsync(int userId)
    {
        return await _context.Set<UserFarm>()
            .AsNoTracking()
            .Include(uf => uf.Farm)
            .Include(uf => uf.FarmRoleType)
            .Where(uf => uf.UserId == userId)
            .ToListAsync();
    }

    public async Task<UserFarm?> GetByUserAndFarmAsync(int userId, int farmId)
    {
        return await _context.Set<UserFarm>()
            .AsNoTracking()
            .Include(uf => uf.FarmRoleType)
            .FirstOrDefaultAsync(uf => uf.UserId == userId && uf.FarmId == farmId);
    }
}