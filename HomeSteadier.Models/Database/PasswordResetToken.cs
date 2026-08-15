using System;
using System.Collections.Generic;

namespace HomeSteadier.Models.Database;

public partial class PasswordResetToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
