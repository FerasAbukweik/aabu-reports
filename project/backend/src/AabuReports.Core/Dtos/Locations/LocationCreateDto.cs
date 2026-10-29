using System.ComponentModel.DataAnnotations;
using AabuReports.Core.ValueObjects;
using AabuReports.Core.Enums;

namespace AabuReports.Core.Dtos.Locations;

public class LocationCreateDto
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public required string Title { get; set; }

    [Required]
    public required MapCoordinates Coordinates { get; set; }

    [Required]
    [EnumDataType(typeof(LocationType))]
    public required LocationType Type { get; set; }
}
