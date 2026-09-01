using HomeSteadier.Models.Database;
using Homesteadier.Repository;
using Microsoft.EntityFrameworkCore;

namespace Homesteadier.Repository.Repositories;

[AutoRegister]
public class FarmInvitationRepository : Repository<FarmInvitation>, IFarmInvitationRepository
{
    public FarmInvitationRepository(HomesteadierDbContext context)
        : base(context)
    {
    }

    public async Task<FarmInvitation?> GetByHashAsync(string tokenHash)
    {
        return await _context.Set<FarmInvitation>()
            .Include(i => i.Farm)
            .Include(i => i.FarmRoleType)
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash);
    }

    public async Task ConsumeAllActiveForFarmAndEmailAsync(int farmId, string email, DateTime when)
    {
        await _context.Set<FarmInvitation>()
            .Where(i => i.FarmId == farmId && i.Email == email && i.AcceptedAt == null && i.ExpiresAt > when)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.AcceptedAt, when));
    }
}