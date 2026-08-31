using System.ComponentModel.DataAnnotations;

namespace HomeSteadier.Models.Request.Auth;

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Set when registering from a farm invitation link. If present, must resolve to an
    /// unaccepted, unexpired invitation whose email matches <see cref="Email"/> — the new account
    /// is then joined to that invitation's farm/role automatically.
    /// </summary>
    public string? InviteToken { get; set; }
}
