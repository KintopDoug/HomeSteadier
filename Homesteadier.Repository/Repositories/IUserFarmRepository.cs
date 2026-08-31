using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

public interface IUserFarmRepository : IRepository<UserFarm>
{
    Task<List<UserFarm>> GetByUserIdAsync(int userId);
}