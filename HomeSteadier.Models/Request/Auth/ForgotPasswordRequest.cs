using System.ComponentModel.DataAnnotations;

namespace HomeSteadier.Models.Request.Auth;

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
