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

    [HttpGet("{farmId:int}/members")]
    public async Task<ActionResult<List<FarmMemberResponse>>> GetMembers(int farmId)
    {
        var userId = User.UserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _farmService.GetMembersAsync(userId.Value, farmId);

        return result.Status switch
        {
            FarmMemberStatus.Success => Ok(result.Value.Select(FarmMemberResponse.FromEntity).ToList()),
            _ => MemberFailure(result.Status),
        };
    }

    [HttpPut("{farmId:int}/members/{memberUserId:int}/role")]
    public async Task<IActionResult> UpdateMemberRole(int farmId, int memberUserId, UpdateFarmMemberRoleRequest request)
    {
        var userId = User.UserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var status = await _farmService.UpdateMemberRoleAsync(userId.Value, farmId, memberUserId, request.FarmRoleTypeId);
        return status == FarmMemberStatus.Success ? NoContent() : MemberFailure(status);
    }

    [HttpDelete("{farmId:int}/members/{memberUserId:int}")]
    public async Task<IActionResult> RemoveMember(int farmId, int memberUserId)
    {
        var userId = User.UserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var status = await _farmService.RemoveMemberAsync(userId.Value, farmId, memberUserId);
        return status == FarmMemberStatus.Success ? NoContent() : MemberFailure(status);
    }

    private ActionResult MemberFailure(FarmMemberStatus status) => status switch
    {
        FarmMemberStatus.NotFarmAdmin => Forbid(),
        FarmMemberStatus.MemberNotFound => NotFound(new { message = "Member not found." }),
        FarmMemberStatus.InvalidRole => BadRequest(new { message = "Invalid role." }),
        FarmMemberStatus.LastAdmin =>
            Conflict(new { message = "A farm must keep at least one admin. Make someone else an admin first." }),
        _ => StatusCode(StatusCodes.Status500InternalServerError),
    };

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
