using System.ComponentModel.DataAnnotations;

namespace HomeSteadier.Models.Request.Farm;

/// <summary>Full replacement of a farm's editable details (PUT semantics).</summary>
public class UpdateFarmRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? AddressLine { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    [Required]
    public decimal Latitude { get; set; }

    [Required]
    public decimal Longitude { get; set; }
}
