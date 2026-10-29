using AabuReports.Core.Enums;
using AabuReports.Core.ValueObjects;

namespace AabuReports.Core.Dtos.Reports;

public class ReportDto
{
    public required Guid Id { get; set; }
    public required DateTime ReportDate { get; set; }
    public required ReportCategory Category { get; set; }
    public required ReportStateEnum State { get; set; }
    public int? Rate { get; set; }
    public required ReportSeverity Severity { get; set; }
    public required string Description { get; set; }
    /// <summary> the name of the location the report is about (so the table does not need the locations list) </summary>
    public required string LocationTitle { get; set; }
    public required MapCoordinates Coordinates { get; set; }
    /// <summary> the urls of the images attached to the report (empty if it has none) </summary>
    public required IReadOnlyList<string> ImageUrls { get; set; }
}