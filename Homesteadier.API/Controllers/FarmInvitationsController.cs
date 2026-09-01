using HomeSteadier.Models.Request.Farm;
using HomeSteadier.Models.Response.Farm;
using Homesteadier.Services.Farms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homesteadier.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FarmInvitationsController : ControllerBase
{
    /// <summary>Every failure mode of an invitation link — unknown, expired, or already accepted —
    /// reports this single message, mirroring AuthController's reset-link handling.</summary>
    private const string InvalidInvitationMessage =
        "This invitation is invalid or has expired. Please ask the farm admin to send a new one.";

    private readonly IFarmInvitationService _invitationService;

    public FarmInvitationsController(IFarmInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(CreateFarmInvitationRequest request)
    {
        var userId = User.UserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var status = await _invitationService.CreateAsync(userId.Value, request);

        return status switch
        {
            CreateFarmInvitationStatus.Success => Accepted(),
            CreateFarmInvitationStatus.NotFarmAdmin => Forbid(),
            CreateFarmInvitationStatus.FarmNotFound => NotFound(new { message = "Farm not found." }),
            CreateFarmInvitationStatus.InvalidRole => BadRequest(new { message = "Invalid role." }),
            CreateFarmInvitationStatus.AlreadyMember =>
                Conflict(new { message = "This person is already a member of the farm." }),
        };
    }

    [AllowAnonymous]
    [HttpGet("{token}")]
    public async Task<ActionResult<FarmInvitationDetailsResponse>> GetByToken(string token)
    {
        var result = await _invitationService.GetByTokenAsync(token);

        return result.Status switch
        {
            FarmInvitationLookupStatus.Success => Ok(Describe(result.Value)),
            FarmInvitationLookupStatus.InvalidOrExpired => NotFound(new { message = InvalidInvitationMessage }),
        };
    }

    [AllowAnonymous]
    [HttpPost("{token}/accept")]
    public async Task<ActionResult<FarmInvitationDetailsResponse>> Accept(string token)
    {
        var result = await _invitationService.AcceptAsync(token);

        return result.Status switch
        {
            FarmInvitationLookupStatus.Success => Ok(Describe(result.Value)),
            FarmInvitationLookupStatus.InvalidOrExpired => BadRequest(new { message = InvalidInvitationMessage }),
        };
    }

    private static FarmInvitationDetailsResponse Describe(FarmInvitationDetails details)
        => FarmInvitationDetailsResponse.FromEntity(details.Invitation, details.AccountExists);
}
