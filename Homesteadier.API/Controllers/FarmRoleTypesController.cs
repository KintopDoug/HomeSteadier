using HomeSteadier.Models.Response.Farm;
using Homesteadier.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homesteadier.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class FarmRoleTypesController : ControllerBase
{
    private readonly IFarmRoleTypeRepository _farmRoleTypeRepository;

    public FarmRoleTypesController(IFarmRoleTypeRepository farmRoleTypeRepository)
    {
        _farmRoleTypeRepository = farmRoleTypeRepository;
    }

    [HttpGet]
    public async Task<ActionResult<List<FarmRoleTypeResponse>>> GetAll()
    {
        var roleTypes = await _farmRoleTypeRepository.GetAllAsync();
        return Ok(roleTypes.Select(FarmRoleTypeResponse.FromEntity).OrderBy(r => r.Name).ToList());
    }
}
