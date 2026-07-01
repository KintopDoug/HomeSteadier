using HomeSteadier.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace Homesteadier.Repository.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(HomesteadierDbContext context)
        : base(context)
    {
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<List<User>> GetActiveUsersAsync()
    {
        return await _context.Users
            .Where(u => u.IsActive)
            .ToListAsync();
    }
}
