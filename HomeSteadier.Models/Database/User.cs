using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HomeSteadier.Models.Database;

public partial class User
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;

    // Stores the ASP.NET Identity password hash. Never serialized out of the API.
    [JsonIgnore]
    public string Password { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsActive { get; set; }
}
