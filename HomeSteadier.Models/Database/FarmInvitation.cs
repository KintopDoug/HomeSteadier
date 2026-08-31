using System;
using System.Collections.Generic;

namespace HomeSteadier.Models.Database;

public partial class FarmInvitation
{
    public int Id { get; set; }

    public int FarmId { get; set; }

    public int FarmRoleTypeId { get; set; }

    public string Email { get; set; } = null!;

    public int InvitedByUserId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public virtual Farm Farm { get; set; } = null!;

    public virtual FarmRoleType FarmRoleType { get; set; } = null!;

    public virtual User InvitedByUser { get; set; } = null!;
}
