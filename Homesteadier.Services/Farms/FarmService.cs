using HomeSteadier.Models.Database;
using HomeSteadier.Models.Request.Farm;
using Homesteadier.Repository.Repositories;
using Microsoft.Extensions.Logging;

namespace Homesteadier.Services.Farms;

public interface IFarmService
{
    /// <summary>The farms the user belongs to, as their memberships (role included).</summary>
    Task<IReadOnlyList<UserFarm>> GetForUserAsync(int userId);

    /// <summary>Creates a farm and makes the creator its admin. Returns the new membership.</summary>
    Task<FarmResult<CreateFarmStatus, UserFarm>> CreateAsync(int userId, CreateFarmRequest request);
}

/// <summary>
/// Farm creation and membership lookup. FarmController maps these results onto HTTP responses and
/// does nothing else.
/// </summary>
internal class FarmService : IFarmService
{
    /// <summary>
    /// The role the creator is granted. Named "Admin" in farm_role_types — it's the same role an
    /// invitation can confer, not a separate "owner" concept.
    /// </summary>
    private const string OwnerRoleName = "Admin";

    private readonly IFarmRepository _farmRepository;
    private readonly IUserFarmRepository _userFarmRepository;
    private readonly IFarmRoleTypeRepository _farmRoleTypeRepository;
    private readonly ILogger<FarmService> _logger;

    public FarmService(
        IFarmRepository farmRepository,
        IUserFarmRepository userFarmRepository,
        IFarmRoleTypeRepository farmRoleTypeRepository,
        ILogger<FarmService> logger)
    {
        _farmRepository = farmRepository;
        _userFarmRepository = userFarmRepository;
        _farmRoleTypeRepository = farmRoleTypeRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserFarm>> GetForUserAsync(int userId)
        => await _userFarmRepository.GetByUserIdAsync(userId);

    public async Task<FarmResult<CreateFarmStatus, UserFarm>> CreateAsync(int userId, CreateFarmRequest request)
    {
        // Look the role up before writing anything: without it the farm would exist with no members
        // and no way to administer it, which is worse than refusing to create it at all.
        var ownerRole = await _farmRoleTypeRepository.GetByNameAsync(OwnerRoleName);
        if (ownerRole is null)
        {
            _logger.LogError(
                "Farm role type \"{RoleName}\" is missing from farm_role_types seed data", OwnerRoleName);
            return FarmResult<CreateFarmStatus, UserFarm>.Failed(CreateFarmStatus.OwnerRoleMissing);
        }

        var farm = new Farm
        {
            Name = request.Name,
            AddressLine = request.AddressLine,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Country = request.Country,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
        };
        await _farmRepository.AddAsync(farm);

        var userFarm = new UserFarm
        {
            UserId = userId,
            // Assigned by navigation rather than by id: farm.Id doesn't exist until the insert, and
            // both entities are tracked by the same scoped DbContext, so EF fills the foreign key in
            // during the save below. Setting the navigations also hydrates them for the caller,
            // which saves a re-read of rows we already have in hand.
            Farm = farm,
            FarmRoleTypeId = ownerRole.Id,
            FarmRoleType = ownerRole,
        };
        await _userFarmRepository.AddAsync(userFarm);

        // One SaveChanges, therefore one transaction. Saving the farm and the membership separately
        // would let the first commit and the second fail, leaving a farm with no members — which
        // GetForUserAsync can never return and no one can administer, so it couldn't be repaired
        // through the API. That's the very state the ownerRole guard above exists to prevent.
        await _userFarmRepository.SaveChangesAsync();

        return FarmResult<CreateFarmStatus, UserFarm>.Succeeded(CreateFarmStatus.Success, userFarm);
    }
}
