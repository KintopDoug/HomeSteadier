using HomeSteadier.Models.Database;

namespace HomeSteadier.Models.Response.Farm;

public class FarmResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? AddressLine { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public static FarmResponse FromEntity(UserFarm userFarm) => new()
    {
        Id = userFarm.Farm.Id,
        Name = userFarm.Farm.Name,
        AddressLine = userFarm.Farm.AddressLine,
        City = userFarm.Farm.City,
        State = userFarm.Farm.State,
        PostalCode = userFarm.Farm.PostalCode,
        Country = userFarm.Farm.Country,
        Latitude = userFarm.Farm.Latitude,
        Longitude = userFarm.Farm.Longitude,
        RoleName = userFarm.FarmRoleType.Name,
        CreatedAt = userFarm.Farm.CreatedAt,
        UpdatedAt = userFarm.Farm.UpdatedAt,
    };
}
