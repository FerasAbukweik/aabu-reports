using aabu_reports.core.DTOs.Location;

namespace aabu_reports.core.DTOs.Report;

public class ReportCreateResponseDTO
{
    public required ReportDTO Report { get; set; }
    public required LocationDTO Location { get; set; }
}