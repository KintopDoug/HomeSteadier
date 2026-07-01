using HomeSteadier.Models.Database;

namespace Homesteadier.Repository.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<List<User>> GetActiveUsersAsync();
}
