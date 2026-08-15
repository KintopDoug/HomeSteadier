using System.ComponentModel.DataAnnotations;

namespace HomeSteadier.Models.Request.Auth;

public class ResetPasswordRequest
{
    /// <summary>The raw token from the emailed reset link's query string.</summary>
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    public string NewPassword { get; set; } = string.Empty;
}
