namespace AabuReports.Core.Dtos.Reports;

public class ReportTableDto
{
    public required IReadOnlyList<ReportDto> Reports { get; set; }

    public required int TotalCount { get; set; }
}
