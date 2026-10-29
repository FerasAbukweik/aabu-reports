using AabuReports.Core.Enums;

namespace AabuReports.Core.Dtos.Reports;

public class ReportForMapDto
{
    public required Guid Id { get; set; }
    public required DateTime ReportDate { get; set; }
    public required ReportCategory Category { get; set; }
    public required ReportStateEnum State { get; set; }
    public required ReportSeverity Severity { get; set; }
    public required string Description { get; set; }
    public required Guid LocationId { get; set; }
}
