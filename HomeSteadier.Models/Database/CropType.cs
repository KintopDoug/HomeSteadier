using System;
using System.Collections.Generic;

namespace HomeSteadier.Models.Database;

public partial class CropType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Genus { get; set; } = null!;

    public string Family { get; set; } = null!;

    public int? SpacingInches { get; set; }

    public decimal? SunlightRequirementHours { get; set; }

    public virtual ICollection<GardenBedCrop> GardenBedCrops { get; set; } = new List<GardenBedCrop>();
}
