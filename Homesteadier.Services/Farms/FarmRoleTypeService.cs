using HomeSteadier.Models.Database;
using Homesteadier.Repository.Repositories;

namespace Homesteadier.Services.Farms;

public interface IFarmRoleTypeService
{
    /// <summary>Every assignable farm role, ordered by name.</summary>
    Task<IReadOnlyList<FarmRoleType>> GetAllAsync();
}

/// <summary>
/// Reference-data lookup for farm roles. Thin by nature — the value of the seam isn't that there
/// are rules to hide today, it's that FarmRoleTypesController no longer reaches into a repository
/// directly, so any rule added later (filtering out roles a caller may not grant, for instance)
/// has an obvious home that doesn't involve touching the controller.
/// </summary>
internal class FarmRoleTypeService : IFarmRoleTypeService
{
    private readonly IFarmRoleTypeRepository _farmRoleTypeRepository;

    public FarmRoleTypeService(IFarmRoleTypeRepository farmRoleTypeRepository)
    {
        _farmRoleTypeRepository = farmRoleTypeRepository;
    }

    public async Task<IReadOnlyList<FarmRoleType>> GetAllAsync()
    {
        var roleTypes = await _farmRoleTypeRepository.GetAllAsync();
        return roleTypes.OrderBy(r => r.Name).ToList();
    }
}
