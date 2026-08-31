using System;
using System.Collections.Generic;

namespace HomeSteadier.Models.Database;

public partial class UserFarm
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int FarmId { get; set; }

    public string Role { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Farm Farm { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
