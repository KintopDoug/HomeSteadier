using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

[AutoRegister]
public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(HomesteadierDbContext context)
        : base(context)
    {
    }

    // Implement custom query methods here. Example:
    // public async Task<User?> GetByIdAsync(int id)
    // {
    //     return await _context.Set<User>()
    //         .FirstOrDefaultAsync(e => e.Id == id);
    // }
}