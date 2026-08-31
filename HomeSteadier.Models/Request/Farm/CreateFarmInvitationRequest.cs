using System.ComponentModel.DataAnnotations;

namespace HomeSteadier.Models.Request.Farm;

public class CreateFarmInvitationRequest
{
    [Required]
    public int FarmId { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public int FarmRoleTypeId { get; set; }
}
