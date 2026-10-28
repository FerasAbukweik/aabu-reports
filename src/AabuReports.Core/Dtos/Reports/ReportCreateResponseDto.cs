using AabuReports.Core.Dtos.Locations;

namespace AabuReports.Core.Dtos.Reports;

public class ReportCreateResponseDto
{
    public required ReportDto Report { get; set; }
    public required LocationDto Location { get; set; }
}