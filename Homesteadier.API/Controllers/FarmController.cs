using HomeSteadier.Models.Request.Farm;
using HomeSteadier.Models.Response.Farm;
using Homesteadier.Services.Farms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homesteadier.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class FarmController : ControllerBase
{
    private readonly IFarmService _farmService;

    public FarmController(IFarmService farmService)
    {
        _farmService = farmService;
    }

    [HttpGet]
    public async Task<ActionResult<List<FarmResponse>>> GetAll()
    {
        var userId = User.UserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var userFarms = await _farmService.GetForUserAsync(userId.Value);
        return Ok(userFarms.Select(FarmResponse.FromEntity).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<FarmResponse>> Create(CreateFarmRequest request)
    {
        var userId = User.UserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _farmService.CreateAsync(userId.Value, request);

        return result.Status switch
        {
            CreateFarmStatus.Success =>
                CreatedAtAction(nameof(GetAll), FarmResponse.FromEntity(result.Value)),
            // Seed data is missing server-side; the caller did nothing wrong and can't fix it.
            CreateFarmStatus.OwnerRoleMissing =>
                StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to create farm" }),
        };
    }
}
