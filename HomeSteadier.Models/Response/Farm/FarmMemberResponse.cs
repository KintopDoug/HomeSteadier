using HomeSteadier.Models.Database;

namespace HomeSteadier.Models.Response.Farm;

public class FarmMemberResponse
{
    public int UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public int FarmRoleTypeId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public static FarmMemberResponse FromEntity(UserFarm userFarm) => new()
    {
        UserId = userFarm.UserId,
        FirstName = userFarm.User.FirstName,
        LastName = userFarm.User.LastName,
        Email = userFarm.User.Email,
        FarmRoleTypeId = userFarm.FarmRoleTypeId,
        RoleName = userFarm.FarmRoleType.Name,
    };
}
