using System.ComponentModel.DataAnnotations;

namespace HomeSteadier.Models.Request.Auth;

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    public string NewPassword { get; set; } = string.Empty;
}
