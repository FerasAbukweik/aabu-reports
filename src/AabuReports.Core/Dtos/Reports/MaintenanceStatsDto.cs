namespace AabuReports.Core.Dtos.Reports;

/// <summary> the numbers shown on the maintenance stats cards </summary>
public class MaintenanceStatsDto
{
    /// <summary> all reports that are fixed </summary>
    public required int CompletedReportsCount { get; set; }

    /// <summary> fixed reports out of all reports sent in the current year, 0 to 100 </summary>
    public required double CompletionRatePercent { get; set; }

    /// <summary> reports that are in progress right now </summary>
    public required int InProgressReportsCount { get; set; }

    /// <summary> average hours between sending a report and fixing it (0 if nothing is fixed yet) </summary>
    public required double AverageResponseTimeHours { get; set; }

    /// <summary> title of the location with the most in-progress reports, null if no report is in progress </summary>
    public string? ActiveCrewLocation { get; set; }
}
