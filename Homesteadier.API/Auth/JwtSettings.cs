namespace Homesteadier.API.Auth;

/// <summary>
/// JWT configuration. Issuer/Audience/ExpiryMinutes are bound from the "Jwt" config section;
/// SigningKey is a secret sourced from the JWT_SIGNING_KEY environment variable.
/// </summary>
public class JwtSettings
{
    public string Issuer { get; set; } = "Homesteadier";

    public string Audience { get; set; } = "Homesteadier";

    public int ExpiryMinutes { get; set; } = 15;

    public int RefreshTokenExpiryDays { get; set; } = 14;

    public string SigningKey { get; set; } = string.Empty;
}
