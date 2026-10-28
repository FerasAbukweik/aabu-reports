using aabu_reports.core.Enums;

namespace aabu_reports.core.DTOs.Report;

public class ReportDTO
{
    public required Guid Id { get; set; }
    public required DateTime ReportDate { get; set; }
    public required ReportCategory Category { get; set; }
    public required ReportState State { get; set; }

    /// <summary> null until the report is fixed and the user rates it </summary>
    public int? Rate { get; set; }

    public required ReportSeverity Severity { get; set; }
    public required string Description { get; set; }
    public required Guid LocationId { get; set; }
    public required Guid UserId { get; set; }
}
