using AabuReports.Core.Dtos.Locations;

namespace AabuReports.Core.Dtos.Reports;

public class ReportCreateResponseDto
{
    /// <summary> the new report, in the shape of the map's location reports list </summary>
    public required ReportMapDto Report { get; set; }
    public required LocationDto Location { get; set; }
}