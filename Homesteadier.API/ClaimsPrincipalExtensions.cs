using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Homesteadier.API;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The signed-in user's id, or null when the token carries no usable "sub" claim.
    ///
    /// Parsing here rather than in each action removes a crash path: the callers previously read
    /// the claim as a string, null-checked it, then called int.Parse — so a token whose "sub"
    /// wasn't an integer produced a FormatException and a 500 instead of the 401 the null check
    /// was clearly meant to give. Returning int? collapses "absent" and "unparseable" into the one
    /// answer every caller already handles.
    /// </summary>
    public static int? UserId(this ClaimsPrincipal principal)
        => int.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : null;
}
