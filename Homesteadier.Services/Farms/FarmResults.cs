using HomeSteadier.Models.Database;

namespace Homesteadier.Services.Farms;

/// <summary>
/// Outcome of an operation that yields a value on success and a status otherwise.
///
/// The payload is a non-nullable accessor over a nullable field, reachable only through the
/// factories — so a success without a value can't be constructed, and reading the value off a
/// failure throws with the status named rather than surfacing as a NullReferenceException a few
/// frames away in a controller.
/// </summary>
public sealed class FarmResult<TStatus, TValue>
    where TStatus : struct, Enum
    where TValue : class
{
    private readonly TValue? _value;

    private FarmResult(TStatus status, TValue? value)
    {
        Status = status;
        _value = value;
    }

    public TStatus Status { get; }

    public TValue Value => _value
        ?? throw new InvalidOperationException(
            $"No value is available — the operation ended as {Status}. Check Status before reading Value.");

    public static FarmResult<TStatus, TValue> Succeeded(TStatus status, TValue value) => new(status, value);

    public static FarmResult<TStatus, TValue> Failed(TStatus status) => new(status, null);
}

public enum CreateFarmStatus
{
    Success,

    /// <summary>
    /// The "Admin" row is missing from the farm_role_types seed data, so a creator can't be given
    /// ownership. A server-side data problem, not anything the caller did wrong.
    /// </summary>
    OwnerRoleMissing,
}

public enum CreateFarmInvitationStatus
{
    Success,

    /// <summary>The inviter isn't an admin of the farm they're inviting to.</summary>
    NotFarmAdmin,

    FarmNotFound,
    InvalidRole,

    /// <summary>The invited address is already a member of this farm.</summary>
    AlreadyMember,
}

public enum FarmInvitationLookupStatus
{
    Success,

    /// <summary>
    /// Unknown, expired, already accepted, or belonging to a missing/deactivated account. One
    /// status covers all of them so a caller can't tell which — the same reasoning behind
    /// AuthController's single password-reset-link message.
    /// </summary>
    InvalidOrExpired,
}

/// <summary>
/// A validated invitation plus whether the invited address already has an account — the two facts
/// a caller needs to choose between an "accept" and a "create your account" flow.
/// </summary>
public sealed class FarmInvitationDetails
{
    public required FarmInvitation Invitation { get; init; }

    public required bool AccountExists { get; init; }
}

public enum SignUpInvitationStatus
{
    Valid,
    InvalidOrExpired,

    /// <summary>The invitation was issued to a different address than the one signing up.</summary>
    EmailMismatch,
}
