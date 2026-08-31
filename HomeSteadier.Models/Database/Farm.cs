using System;
using System.Collections.Generic;

namespace HomeSteadier.Models.Database;

public partial class Farm
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? AddressLine { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<UserFarm> UserFarms { get; set; } = new List<UserFarm>();
}
