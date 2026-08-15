namespace Homesteadier.API.Auth;

/// <summary>
/// Password-reset token configuration, bound from the "PasswordReset" config section.
/// </summary>
public class PasswordResetSettings
{
    /// <summary>
    /// How long an emailed reset link stays usable. Sixty rather than fifteen minutes because
    /// mail delivery plus a user switching to their mail client routinely eats several minutes,
    /// and the token is single-use, high-entropy, and superseded by the next request anyway.
    /// </summary>
    public int TokenExpiryMinutes { get; set; } = 60;
}
