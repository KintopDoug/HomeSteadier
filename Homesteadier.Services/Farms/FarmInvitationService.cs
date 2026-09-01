using HomeSteadier.Models.Database;
using HomeSteadier.Models.Request.Farm;
using Homesteadier.Repository.Repositories;
using Homesteadier.Services.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Homesteadier.Services.Farms;

public interface IFarmInvitationService
{
    /// <summary>
    /// Issues an invitation on behalf of <paramref name="invitedByUserId"/> and emails the link,
    /// after checking the inviter administers the farm and the invitee isn't already a member.
    /// </summary>
    Task<CreateFarmInvitationStatus> CreateAsync(int invitedByUserId, CreateFarmInvitationRequest request);

    /// <summary>Validates a raw invitation token without consuming it.</summary>
    Task<FarmResult<FarmInvitationLookupStatus, FarmInvitationDetails>> GetByTokenAsync(string rawToken);

    /// <summary>Joins the already-registered invitee to the farm and consumes the invitation.</summary>
    Task<FarmResult<FarmInvitationLookupStatus, FarmInvitationDetails>> AcceptAsync(string rawToken);
}

/// <summary>
/// The invitation operations the sign-up flow needs, kept apart from
/// <see cref="IFarmInvitationService"/> because the two have disjoint callers: AuthController uses
/// only these two, FarmInvitationsController uses only the other three. One combined interface
/// would force each to depend on methods it never calls.
///
/// Both are implemented by the same class — the split is about what callers can see, not about
/// splitting the behaviour.
/// </summary>
public interface ISignUpInvitationService
{
    /// <summary>
    /// Checks an invitation carried through the sign-up form, without consuming it. Separate from
    /// <see cref="JoinAndConsumeAsync"/> because sign-up must validate the invitation *before*
    /// creating the account (so a stale link fails loudly rather than silently registering someone
    /// into no farm) and join them *after* the account exists.
    /// </summary>
    Task<FarmResult<SignUpInvitationStatus, FarmInvitation>> ValidateForSignUpAsync(string rawToken, string email);

    /// <summary>
    /// Adds the freshly-created user to the invited farm and consumes the invitation. Pair with
    /// <see cref="ValidateForSignUpAsync"/>.
    /// </summary>
    Task JoinAndConsumeAsync(int userId, FarmInvitation invitation);
}

/// <summary>
/// Owns the farm-invitation rules: who may invite, who may be invited, and what happens when a
/// link is used. FarmInvitationsController maps these results onto HTTP responses; AuthController
/// uses the sign-up pair so that it never has to touch the invitation token service or the
/// membership repository itself.
///
/// The token service, its settings, and the invitation email template are all internal to this
/// assembly — these two interfaces are the only way to reach them.
/// </summary>
internal class FarmInvitationService : IFarmInvitationService, ISignUpInvitationService
{
    private const string AdminRoleName = "Admin";

    private readonly UserManager<User> _userManager;
    private readonly IFarmRepository _farmRepository;
    private readonly IFarmRoleTypeRepository _farmRoleTypeRepository;
    private readonly IUserFarmRepository _userFarmRepository;
    private readonly IFarmInvitationTokenService _invitationTokenService;
    private readonly IEmailSender _emailSender;
    private readonly FrontendUrls _frontendUrls;
    private readonly FarmInvitationSettings _settings;
    private readonly ILogger<FarmInvitationService> _logger;

    public FarmInvitationService(
        UserManager<User> userManager,
        IFarmRepository farmRepository,
        IFarmRoleTypeRepository farmRoleTypeRepository,
        IUserFarmRepository userFarmRepository,
        IFarmInvitationTokenService invitationTokenService,
        IEmailSender emailSender,
        FrontendUrls frontendUrls,
        FarmInvitationSettings settings,
        ILogger<FarmInvitationService> logger)
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

    public async Task<CreateFarmInvitationStatus> CreateAsync(int invitedByUserId, CreateFarmInvitationRequest request)
    {
        var inviterMembership = await _userFarmRepository.GetByUserAndFarmAsync(invitedByUserId, request.FarmId);
        if (inviterMembership is null || inviterMembership.FarmRoleType.Name != AdminRoleName)
        {
            return CreateFarmInvitationStatus.NotFarmAdmin;
        }

        var farm = await _farmRepository.GetByIdAsync(request.FarmId);
        if (farm is null)
        {
            return CreateFarmInvitationStatus.FarmNotFound;
        }

        var roleType = await _farmRoleTypeRepository.GetByIdAsync(request.FarmRoleTypeId);
        if (roleType is null)
        {
            return CreateFarmInvitationStatus.InvalidRole;
        }

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            var existingMembership = await _userFarmRepository.GetByUserAndFarmAsync(existingUser.Id, request.FarmId);
            if (existingMembership is not null)
            {
                return CreateFarmInvitationStatus.AlreadyMember;
            }
        }

        var (rawToken, _) = await _invitationTokenService.IssueAsync(
            request.FarmId, request.Email, request.FarmRoleTypeId, invitedByUserId);

        // An address with no account can't accept directly — point it at registration with the
        // token attached, so signing up and joining the farm are a single step.
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
            // The invitation is issued and the link works; a transport failure shouldn't report as
            // "the invite wasn't created". Logged so it can be chased and re-sent.
            _logger.LogError(ex, "Failed to send a farm invitation email for farm {FarmId}.", request.FarmId);
        }

        return CreateFarmInvitationStatus.Success;
    }

    public async Task<FarmResult<FarmInvitationLookupStatus, FarmInvitationDetails>> GetByTokenAsync(string rawToken)
    {
        var invitation = await _invitationTokenService.ValidateAsync(rawToken);
        if (invitation is null)
        {
            return Invalid();
        }

        var accountExists = await _userManager.FindByEmailAsync(invitation.Email) is not null;
        return Found(invitation, accountExists);
    }

    public async Task<FarmResult<FarmInvitationLookupStatus, FarmInvitationDetails>> AcceptAsync(string rawToken)
    {
        var invitation = await _invitationTokenService.ValidateAsync(rawToken);
        if (invitation is null)
        {
            return Invalid();
        }

        var user = await _userManager.FindByEmailAsync(invitation.Email);
        if (user is null || !user.IsActive)
        {
            return Invalid();
        }

        // Tolerate a repeat click: if they're already a member, still consume the token and report
        // success rather than failing someone who opened the link twice.
        var existingMembership = await _userFarmRepository.GetByUserAndFarmAsync(user.Id, invitation.FarmId);
        if (existingMembership is null)
        {
            await AddMembershipAsync(user.Id, invitation);
        }

        await _invitationTokenService.ConsumeAsync(invitation);

        return Found(invitation, accountExists: true);
    }

    public async Task<FarmResult<SignUpInvitationStatus, FarmInvitation>> ValidateForSignUpAsync(
        string rawToken, string email)
    {
        var invitation = await _invitationTokenService.ValidateAsync(rawToken);
        if (invitation is null)
        {
            return FarmResult<SignUpInvitationStatus, FarmInvitation>.Failed(SignUpInvitationStatus.InvalidOrExpired);
        }

        if (!string.Equals(invitation.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            return FarmResult<SignUpInvitationStatus, FarmInvitation>.Failed(SignUpInvitationStatus.EmailMismatch);
        }

        return FarmResult<SignUpInvitationStatus, FarmInvitation>.Succeeded(SignUpInvitationStatus.Valid, invitation);
    }

    public async Task JoinAndConsumeAsync(int userId, FarmInvitation invitation)
    {
        await AddMembershipAsync(userId, invitation);
        await _invitationTokenService.ConsumeAsync(invitation);
    }

    private async Task AddMembershipAsync(int userId, FarmInvitation invitation)
    {
        await _userFarmRepository.AddAsync(new UserFarm
        {
            UserId = userId,
            FarmId = invitation.FarmId,
            FarmRoleTypeId = invitation.FarmRoleTypeId,
        });
        await _userFarmRepository.SaveChangesAsync();
    }

    private static FarmResult<FarmInvitationLookupStatus, FarmInvitationDetails> Invalid()
        => FarmResult<FarmInvitationLookupStatus, FarmInvitationDetails>.Failed(
            FarmInvitationLookupStatus.InvalidOrExpired);

    private static FarmResult<FarmInvitationLookupStatus, FarmInvitationDetails> Found(
        FarmInvitation invitation, bool accountExists)
        => FarmResult<FarmInvitationLookupStatus, FarmInvitationDetails>.Succeeded(
            FarmInvitationLookupStatus.Success,
            new FarmInvitationDetails { Invitation = invitation, AccountExists = accountExists });
}
