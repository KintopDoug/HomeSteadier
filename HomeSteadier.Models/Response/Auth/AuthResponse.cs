using HomeSteadier.Models.Database;

namespace HomeSteadier.Models.Response.Auth;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public User User { get; set; } = null!;
}
