namespace aabu_reports.core.Enums;

// ordered by importance: the bigger the number, the more attention it needs.
// A location shows the highest state among its reports (Normal when it has none),
// so the order matters, and the numbers are what is stored in the DB.
public enum ReportState {
    Normal,
    Fixed,
    InProgress,
    Pending,
    NeedsFollowUp,
}
