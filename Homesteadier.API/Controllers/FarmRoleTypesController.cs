using HomeSteadier.Models.Response.Farm;
using Homesteadier.Services.Farms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homesteadier.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class FarmRoleTypesController : ControllerBase
{
    private readonly IFarmRoleTypeService _farmRoleTypeService;

    public FarmRoleTypesController(IFarmRoleTypeService farmRoleTypeService)
    {
        _farmRoleTypeService = farmRoleTypeService;
    }

    [HttpGet]
    public async Task<ActionResult<List<FarmRoleTypeResponse>>> GetAll()
    {
        var roleTypes = await _farmRoleTypeService.GetAllAsync();
        return Ok(roleTypes.Select(FarmRoleTypeResponse.FromEntity).ToList());
    }
}
