using System;

namespace HomeSteadier.Models.Security;

/// <summary>
/// Claim types present in Clerk-issued session JWTs. Encapsulates the on-the-wire
/// claim keys so callers reference these members instead of magic strings.
/// </summary>
public enum ClaimTypes
{
    /// <summary>The subject — the Clerk user id ("sub").</summary>
    Sub,

    /// <summary>The authorized party — the origin the token was minted for ("azp").</summary>
    Azp,
}

public static class ClaimTypesExtensions
{
    /// <summary>
    /// The on-the-wire claim key for a <see cref="ClaimTypes"/> value, e.g. the string
    /// passed to <c>ClaimsPrincipal.FindFirst</c>.
    /// </summary>
    public static string Value(this ClaimTypes claimType) => claimType switch
    {
        ClaimTypes.Sub => "sub",
        ClaimTypes.Azp => "azp",
        _ => throw new ArgumentOutOfRangeException(nameof(claimType), claimType, null),
    };
}
