using HomeSteadier.Models.Database;
using Homesteadier.Repository;

namespace Homesteadier.Repository.Repositories;

public interface IFarmRoleTypeRepository : IRepository<FarmRoleType>
{
    Task<FarmRoleType?> GetByNameAsync(string name);
}