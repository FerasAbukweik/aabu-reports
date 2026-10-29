namespace AabuReports.Core.Dtos.Reports;

/// <summary> the numbers shown on the stats cards of the reports history page, all about the reports of one user </summary>
public class ReportHistoryStatsDto
{
    /// <summary> every report the user sent (the history table uses it to know how many pages it has) </summary>
    public required int TotalReportsCount { get; set; }

    /// <summary> the user's reports that are in progress right now </summary>
    public required int InProgressReportsCount { get; set; }

    /// <summary> the user's reports that are fixed </summary>
    public required int CompletedReportsCount { get; set; }

    /// <summary> the user's fixed reports that the user did not rate yet </summary>
    public required int AwaitingRatingReportsCount { get; set; }
}
