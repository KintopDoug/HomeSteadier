using System;
using System.Collections.Generic;

namespace HomeSteadier.Models.Database;

public partial class GardenBed
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal Length { get; set; }

    public decimal Width { get; set; }

    public decimal SunlightHours { get; set; }

    public virtual ICollection<GardenBedCrop> GardenBedCrops { get; set; } = new List<GardenBedCrop>();
}
