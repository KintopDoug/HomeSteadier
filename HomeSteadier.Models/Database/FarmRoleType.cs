using System;
using System.Collections.Generic;

namespace HomeSteadier.Models.Database;

public partial class FarmRoleType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<UserFarm> UserFarms { get; set; } = new List<UserFarm>();
}
