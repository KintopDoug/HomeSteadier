using HomeSteadier.Models.Database;

namespace HomeSteadier.Models.Response.Farm;

public class FarmRoleTypeResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public static FarmRoleTypeResponse FromEntity(FarmRoleType roleType) => new()
    {
        Id = roleType.Id,
        Name = roleType.Name,
    };
}
