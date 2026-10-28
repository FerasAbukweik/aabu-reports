using System.ComponentModel.DataAnnotations;
using aabu_reports.core.Classes;
using aabu_reports.core.Enums;

namespace aabu_reports.core.DTOs.Location;

public class LocationUpdateDTO
{
    [Required]
    public required Guid Id { get; set; }
    
    [StringLength(150, MinimumLength = 2)]
    public string? Title { get; set; }

    [EnumDataType(typeof(LocationTypeEnum))]
    public LocationTypeEnum? Type { get; set; }
    
    public MapCoordinates? Coordinates { get; set; }
    
}
