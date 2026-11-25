using System.ComponentModel.DataAnnotations;

namespace HomeSteadier.Models.Request.Farm;

public class UpdateFarmMemberRoleRequest
{
    [Required]
    public int FarmRoleTypeId { get; set; }
}
