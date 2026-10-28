using System.ComponentModel.DataAnnotations;
using aabu_reports.core.Classes;
using aabu_reports.core.Enums;

namespace aabu_reports.core.DTOs.Location;

public class LocationCreateDTO
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public required string Title { get; set; }

    [Required]
    public required MapCoordinates Coordinates { get; set; }

    [Required]
    [EnumDataType(typeof(LocationTypeEnum))]
    public required LocationTypeEnum Type { get; set; }
}
