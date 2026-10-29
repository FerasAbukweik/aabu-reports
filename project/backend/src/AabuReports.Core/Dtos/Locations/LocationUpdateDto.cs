using System.ComponentModel.DataAnnotations;
using AabuReports.Core.ValueObjects;
using AabuReports.Core.Enums;

namespace AabuReports.Core.Dtos.Locations;

public class LocationUpdateDto
{
    [Required]
    public required Guid Id { get; set; }
    
    [StringLength(150, MinimumLength = 2)]
    public string? Title { get; set; }

    [EnumDataType(typeof(LocationType))]
    public LocationType? Type { get; set; }
    
    public MapCoordinates? Coordinates { get; set; }
    
}
