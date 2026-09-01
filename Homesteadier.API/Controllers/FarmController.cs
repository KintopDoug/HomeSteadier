using HomeSteadier.Models.Database;
using HomeSteadier.Models.Request.Farm;
using HomeSteadier.Models.Response.Farm;
using Homesteadier.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Homesteadier.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class FarmController : ControllerBase
{
    private const string OwnerRoleName = "Admin";

    private readonly IFarmRepository _farmRepository;
    private readonly IUserFarmRepository _userFarmRepository;
    private readonly IFarmRoleTypeRepository _farmRoleTypeRepository;
    private readonly ILogger<FarmController> _logger;

    public FarmController(
        IFarmRepository farmRepository,
        IUserFarmRepository userFarmRepository,
        IFarmRoleTypeRepository farmRoleTypeRepository,
        ILogger<FarmController> logger)
    {
        _farmRepository = farmRepository;
        _userFarmRepository = userFarmRepository;
        _farmRoleTypeRepository = farmRoleTypeRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<FarmResponse>>> GetAll()
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId is null)
        {
            return Unauthorized();
        }

        var userFarms = await _userFarmRepository.GetByUserIdAsync(int.Parse(userId));
        return Ok(userFarms.Select(FarmResponse.FromEntity).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<FarmResponse>> Create(CreateFarmRequest request)
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId is null)
        {
            return Unauthorized();
        }

        var ownerRole = await _farmRoleTypeRepository.GetByNameAsync(OwnerRoleName);
        if (ownerRole is null)
        {
            _logger.LogError("Farm role type \"{RoleName}\" is missing from farm_role_types seed data", OwnerRoleName);
            return StatusCode(500, new { message = "Unable to create farm" });
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
        await _farmRepository.SaveChangesAsync();

        var userFarm = new UserFarm
        {
            UserId = int.Parse(userId),
            FarmId = farm.Id,
            FarmRoleTypeId = ownerRole.Id,
        };
        await _userFarmRepository.AddAsync(userFarm);
        await _userFarmRepository.SaveChangesAsync();

        userFarm.Farm = farm;
        userFarm.FarmRoleType = ownerRole;

        return CreatedAtAction(nameof(GetAll), FarmResponse.FromEntity(userFarm));
    }
}
