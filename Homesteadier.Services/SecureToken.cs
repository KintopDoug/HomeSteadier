using System.Security.Cryptography;
using System.Text;

namespace Homesteadier.Services;

/// <summary>
/// Opaque bearer-secret primitives shared by the refresh-token and password-reset flows:
/// 32 cryptographically-random bytes, base64url-encoded so the value is safe in both a cookie
/// and a URL query string, with only the SHA-256 digest ever persisted (a database leak yields
/// no usable token).
///
/// The hash format is load-bearing: <see cref="Hash"/> must stay byte-identical to what issued
/// the tokens currently in the database. Changing the algorithm, or the casing that
/// <see cref="Convert.ToHexString"/> produces, silently invalidates every stored token — every
/// signed-in user is logged out and nothing anywhere reports an error.
/// </summary>
public static class SecureToken
{
    public static string Generate()
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
