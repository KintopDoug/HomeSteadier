using Homesteadier.API.Auth;
using Homesteadier.API.Email;
using Homesteadier.API.Farms;
using HomeSteadier.Models.Database;
using HomeSteadier.Models.Request.Farm;
using HomeSteadier.Models.Response.Farm;
using Homesteadier.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Homesteadier.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FarmInvitationsController : ControllerBase
{
    private const string AdminRoleName = "Admin";

    /// <summary>Every failure mode of an invitation link — unknown, expired, or already accepted —
    /// reports this single message, mirroring AuthController's reset-link handling.</summary>
    private const string InvalidInvitationMessage =
        "This invitation is invalid or has expired. Please ask the farm admin to send a new one.";

    private readonly UserManager<User> _userManager;
    private readonly IFarmRepository _farmRepository;
    private readonly IFarmRoleTypeRepository _farmRoleTypeRepository;
    private readonly IUserFarmRepository _userFarmRepository;
    private readonly IFarmInvitationTokenService _invitationTokenService;
    private readonly IEmailSender _emailSender;
    private readonly FrontendUrls _frontendUrls;
    private readonly FarmInvitationSettings _settings;
    private readonly ILogger<FarmInvitationsController> _logger;

    public FarmInvitationsController(
        UserManager<User> userManager,
        IFarmRepository farmRepository,
        IFarmRoleTypeRepository farmRoleTypeRepository,
        IUserFarmRepository userFarmRepository,
        IFarmInvitationTokenService invitationTokenService,
        IEmailSender emailSender,
        FrontendUrls frontendUrls,
        FarmInvitationSettings settings,
        ILogger<FarmInvitationsController> logger)
    {
        _userManager = userManager;
        _farmRepository = farmRepository;
        _farmRoleTypeRepository = farmRoleTypeRepository;
        _userFarmRepository = userFarmRepository;
        _invitationTokenService = invitationTokenService;
        _emailSender = emailSender;
        _frontendUrls = frontendUrls;
        _settings = settings;
        _logger = logger;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(CreateFarmInvitationRequest request)
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId is null)
        {
            return Unauthorized();
        }

        var inviterMembership = await _userFarmRepository.GetByUserAndFarmAsync(int.Parse(userId), request.FarmId);
        if (inviterMembership is null || inviterMembership.FarmRoleType.Name != AdminRoleName)
        {
            return Forbid();
        }

        var farm = await _farmRepository.GetByIdAsync(request.FarmId);
        if (farm is null)
        {
            return NotFound(new { message = "Farm not found." });
        }

        var roleType = await _farmRoleTypeRepository.GetByIdAsync(request.FarmRoleTypeId);
        if (roleType is null)
        {
            return BadRequest(new { message = "Invalid role." });
        }

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            var existingMembership = await _userFarmRepository.GetByUserAndFarmAsync(existingUser.Id, request.FarmId);
            if (existingMembership is not null)
            {
                return Conflict(new { message = "This person is already a member of the farm." });
            }
        }

        var (rawToken, _) = await _invitationTokenService.IssueAsync(
            request.FarmId, request.Email, request.FarmRoleTypeId, int.Parse(userId));

        var (subject, htmlBody, plainTextBody) = existingUser is not null
            ? FarmInvitationEmail.ComposeForExistingUser(
                farm.Name, roleType.Name, _frontendUrls.AcceptFarmInvitationLink(rawToken), _settings.TokenExpiryDays)
            : FarmInvitationEmail.ComposeForNewUser(
                farm.Name, roleType.Name, _frontendUrls.RegisterWithInviteLink(rawToken), _settings.TokenExpiryDays);

        try
        {
            await _emailSender.SendAsync(request.Email, subject, htmlBody, plainTextBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send a farm invitation email for farm {FarmId}.", request.FarmId);
        }

        return Accepted();
    }

    [AllowAnonymous]
    [HttpGet("{token}")]
    public async Task<ActionResult<FarmInvitationDetailsResponse>> GetByToken(string token)
    {
        var invitation = await _invitationTokenService.ValidateAsync(token);
        if (invitation is null)
        {
            return NotFound(new { message = InvalidInvitationMessage });
        }

        var accountExists = await _userManager.FindByEmailAsync(invitation.Email) is not null;
        return Ok(FarmInvitationDetailsResponse.FromEntity(invitation, accountExists));
    }

    [AllowAnonymous]
    [HttpPost("{token}/accept")]
    public async Task<ActionResult<FarmInvitationDetailsResponse>> Accept(string token)
    {
        var invitation = await _invitationTokenService.ValidateAsync(token);
        if (invitation is null)
        {
            return BadRequest(new { message = InvalidInvitationMessage });
        }

        var user = await _userManager.FindByEmailAsync(invitation.Email);
        if (user is null || !user.IsActive)
        {
            return BadRequest(new { message = InvalidInvitationMessage });
        }

        var existingMembership = await _userFarmRepository.GetByUserAndFarmAsync(user.Id, invitation.FarmId);
        if (existingMembership is null)
        {
            await _userFarmRepository.AddAsync(new UserFarm
            {
                UserId = user.Id,
                FarmId = invitation.FarmId,
                FarmRoleTypeId = invitation.FarmRoleTypeId,
            });
            await _userFarmRepository.SaveChangesAsync();
        }

        await _invitationTokenService.ConsumeAsync(invitation);

        return Ok(FarmInvitationDetailsResponse.FromEntity(invitation, accountExists: true));
    }
}
