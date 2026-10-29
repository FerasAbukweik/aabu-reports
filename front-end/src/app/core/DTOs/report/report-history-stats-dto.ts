/** GET /report/table/stats (any logged-in user): the numbers of the stats cards of the reports table, about the reports the table shows (the current user's, or everyone's for an admin who asked for all) */
export interface ReportHistoryStatsDTO {
  /** every report the user sent. It also decides how many pages the history table has */
  totalReportsCount: number;
  /** the user's reports whose state is InProgress */
  inProgressReportsCount: number;
  /** the user's reports whose state is Fixed */
  completedReportsCount: number;
  /** the user's Fixed reports that the user did not rate yet (Rate is null) */
  awaitingRatingReportsCount: number;
}
