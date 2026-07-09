using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

public interface IUserRepository : IRepository<User>
{
    // Example custom query - uncomment and modify for your needs:
    // Task<User?> GetByIdAsync(int id);
    //
    // Example filtered collection - uncomment and modify:
    // Task<List<User>> GetActiveAsync();
}