using HomeSteadier.Models.Response.Users;

namespace HomeSteadier.Models.Response.Auth;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public UserResponse User { get; set; } = null!;
}
