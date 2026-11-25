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

    public async Task<List<UserFarm>> GetByFarmIdAsync(int farmId)
    {
        return await _context.Set<UserFarm>()
            .AsNoTracking()
            .Include(uf => uf.User)
            .Include(uf => uf.FarmRoleType)
            .Where(uf => uf.FarmId == farmId)
            .OrderBy(uf => uf.User.FirstName)
            .ThenBy(uf => uf.User.LastName)
            .ThenBy(uf => uf.Id)
            .ToListAsync();
    }

    public async Task<UserFarm?> GetTrackedByUserAndFarmAsync(int userId, int farmId)
    {
        return await _context.Set<UserFarm>()
            .Include(uf => uf.FarmRoleType)
            .FirstOrDefaultAsync(uf => uf.UserId == userId && uf.FarmId == farmId);
    }

    public async Task<int> CountByFarmAndRoleAsync(int farmId, int farmRoleTypeId)
    {
        return await _context.Set<UserFarm>()
            .CountAsync(uf => uf.FarmId == farmId && uf.FarmRoleTypeId == farmRoleTypeId);
    }
}
