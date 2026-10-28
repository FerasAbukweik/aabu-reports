using AabuReports.Core.ValueObjects;
using AabuReports.Core.Enums;

namespace AabuReports.Core.Dtos.Locations;

public class LocationDto
{
    public required Guid Id { get; set; } = Guid.NewGuid();

    /// <summary> the highest state among the location's reports, Normal if it has none </summary>
    public required ReportState Status { get; set; }
    public required string Title { get; set; }
    public required MapCoordinates Coordinates { get; set; }
    public required LocationType Type { get; set; }
    public required int ReportsCount { get; set; }
}
