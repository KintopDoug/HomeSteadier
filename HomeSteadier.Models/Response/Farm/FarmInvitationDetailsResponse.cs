using HomeSteadier.Models.Database;

namespace HomeSteadier.Models.Response.Farm;

public class FarmInvitationDetailsResponse
{
    public string FarmName { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool AccountExists { get; set; }

    public static FarmInvitationDetailsResponse FromEntity(FarmInvitation invitation, bool accountExists) => new()
    {
        FarmName = invitation.Farm.Name,
        RoleName = invitation.FarmRoleType.Name,
        Email = invitation.Email,
        AccountExists = accountExists,
    };
}
