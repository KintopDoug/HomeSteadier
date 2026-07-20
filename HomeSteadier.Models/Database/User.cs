using System;
using System.Collections.Generic;

namespace HomeSteadier.Models.Database;

public partial class User
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsActive { get; set; }

    public string? ClerkUserId { get; set; }
}
