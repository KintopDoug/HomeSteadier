using HomeSteadier.Models.Database;
using Homesteadier.Repository;
using Microsoft.EntityFrameworkCore;

namespace Homesteadier.Repository.Repositories;

[AutoRegister]
public class PasswordResetTokenRepository : Repository<PasswordResetToken>, IPasswordResetTokenRepository
{
    public PasswordResetTokenRepository(HomesteadierDbContext context)
        : base(context)
    {
    }

    public async Task<PasswordResetToken?> GetByHashAsync(string tokenHash)
        => await _context.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

    public async Task ConsumeAllActiveForUserAsync(int userId, DateTime when)
    {
        await _context.PasswordResetTokens
            .Where(t => t.UserId == userId && t.ConsumedAt == null && t.ExpiresAt > when)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.ConsumedAt, when));
    }
}
