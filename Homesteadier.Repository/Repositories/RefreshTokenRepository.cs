using HomeSteadier.Models.Database;
using Homesteadier.Repository;
using Microsoft.EntityFrameworkCore;

namespace Homesteadier.Repository.Repositories;

[AutoRegister]
public class RefreshTokenRepository : Repository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(HomesteadierDbContext context)
        : base(context)
    {
    }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash)
    {
        return await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
    }

    public async Task RevokeAllActiveForUserAsync(int userId, DateTime when)
    {
        await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > when)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, when));
    }
}
