namespace Homesteadier.API.Auth;

/// <summary>
/// Rate-limit policy applied to the endpoints that accept raw credentials. Without it there is
/// no brute-force protection at all: the custom <c>UserStore</c> implements no
/// <c>IUserLockoutStore</c> and <c>SignInManager</c> isn't used, so ASP.NET Identity's account
/// lockout never engages. Configured in Program.cs.
/// </summary>
public static class AuthRateLimiting
{
    public const string PolicyName = "auth";

    /// <summary>Requests allowed per <see cref="Window"/>, per client IP.</summary>
    public const int PermitLimit = 10;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
}
