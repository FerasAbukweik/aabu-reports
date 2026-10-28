using aabu_reports.core.Classes;
using aabu_reports.core.Enums;

namespace aabu_reports.core.DTOs.Location;

public class LocationDTO
{
    public required Guid Id { get; set; } = Guid.NewGuid();

    /// <summary> the highest state among the location's reports, Normal if it has none </summary>
    public required ReportState Status { get; set; }
    public required string Title { get; set; }
    public required MapCoordinates Coordinates { get; set; }
    public required LocationTypeEnum Type { get; set; }
    public required int ReportsCount { get; set; }
}
