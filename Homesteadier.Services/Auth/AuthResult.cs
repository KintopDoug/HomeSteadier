using Microsoft.AspNetCore.Identity;

namespace Homesteadier.Services.Auth;

public enum AuthStatus
{
    Success,

    /// <summary>Sign-up: an account with that email already exists.</summary>
    EmailTaken,

    /// <summary>Sign-up: the invitation is unknown, expired, or already used.</summary>
    InvalidInvitation,

    /// <summary>Sign-up: the invitation was issued to a different address than the one signing up.</summary>
    InvitationEmailMismatch,

    /// <summary>
    /// ASP.NET Identity rejected the operation (password policy, duplicate email, ...).
    /// <see cref="AuthResult.Errors"/> carries what it reported.
    /// </summary>
    IdentityFailed,

    /// <summary>Login: unknown email, deactivated account, or wrong password — one status so callers can't tell which.</summary>
    InvalidCredentials,

    /// <summary>Refresh: the token is missing, unknown, expired, revoked, or its user is gone or deactivated.</summary>
    InvalidRefreshToken,

    /// <summary>
    /// Reset: unknown, expired, already used, superseded, or belonging to a deactivated account. One status
    /// covers all of them — distinguishing them would tell an attacker which tokens exist.
    /// </summary>
    InvalidResetLink,

    /// <summary>Change password: the supplied current password is wrong.</summary>
    IncorrectCurrentPassword,

    /// <summary>The signed-in user no longer exists or has been deactivated.</summary>
    Unauthenticated,

    /// <summary>The user does not exist.</summary>
    NotFound,
}

/// <summary>
/// Outcome of an auth operation that yields no value. Unlike the farm results, it can carry the
/// <see cref="IdentityError"/>s ASP.NET Identity reported, so a controller can echo them back.
/// </summary>
public class AuthResult
{
    private protected AuthResult(AuthStatus status, IReadOnlyList<IdentityError>? errors)
    {
        Status = status;
        Errors = errors ?? [];
    }

    public AuthStatus Status { get; }

    public IReadOnlyList<IdentityError> Errors { get; }

    public bool Succeeded => Status == AuthStatus.Success;

    public static AuthResult Success() => new(AuthStatus.Success, null);

    public static AuthResult Failed(AuthStatus status) => new(status, null);
}

/// <summary>
/// An <see cref="AuthResult"/> that also carries a value on success. Reading <see cref="Value"/> off a
/// failure throws with the status named, rather than surfacing as a NullReferenceException in a controller.
/// </summary>
public sealed class AuthResult<TValue> : AuthResult
    where TValue : class
{
    private readonly TValue? _value;

    private AuthResult(AuthStatus status, TValue? value, IReadOnlyList<IdentityError>? errors)
        : base(status, errors)
    {
        _value = value;
    }

    public TValue Value => _value
        ?? throw new InvalidOperationException(
            $"No value is available — the operation ended as {Status}. Check Status before reading Value.");

    public static AuthResult<TValue> Success(TValue value) => new(AuthStatus.Success, value, null);

    public new static AuthResult<TValue> Failed(AuthStatus status) => new(status, null, null);

    public static AuthResult<TValue> Failed(AuthStatus status, IEnumerable<IdentityError> errors)
        => new(status, null, errors.ToList());
}
