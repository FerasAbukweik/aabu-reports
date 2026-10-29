/** GET /report/mine/stats (any logged-in user): the numbers of the stats cards of the history page, all about the reports of the current user */
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
