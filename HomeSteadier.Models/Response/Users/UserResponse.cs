using HomeSteadier.Models.Database;

namespace HomeSteadier.Models.Response.Users;

/// <summary>
/// Public projection of <see cref="User"/>. Exists so the EF entity is never serialized to
/// clients directly: doing so exposes the <c>password</c> column (the stored hash) and, when
/// EF has the navigation loaded, the <c>RefreshTokens</c> collection including token hashes.
/// Always map through here rather than returning <see cref="User"/> from a controller.
/// </summary>
public class UserResponse
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public static UserResponse FromEntity(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
    };
}
