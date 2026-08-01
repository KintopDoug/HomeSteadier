using System;
using System.Text.Json.Serialization;

namespace HomeSteadier.Models.Database;

public partial class RefreshToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    // SHA-256 hash of the raw refresh token. The raw value only ever lives in the client
    // cookie; only this hash is persisted, and it is never serialized out of the API.
    [JsonIgnore]
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? ReplacedByHash { get; set; }

    /// <summary>Active = not yet revoked and not yet expired.</summary>
    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}
