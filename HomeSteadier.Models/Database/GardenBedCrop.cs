using System;
using System.Collections.Generic;

namespace HomeSteadier.Models.Database;

public partial class GardenBedCrop
{
    public int Id { get; set; }

    public int GardenBedId { get; set; }

    public int CropTypeId { get; set; }

    public virtual CropType CropType { get; set; } = null!;

    public virtual GardenBed GardenBed { get; set; } = null!;
}
