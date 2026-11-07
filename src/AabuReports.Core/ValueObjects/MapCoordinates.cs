using System.ComponentModel.DataAnnotations;

namespace AabuReports.Core.ValueObjects;

public class MapCoordinates
{
    [Range(-90, 90)]
    public required double Lat { get; set; }

    [Range(-180, 180)]
    public required double Lng { get; set; }
}
