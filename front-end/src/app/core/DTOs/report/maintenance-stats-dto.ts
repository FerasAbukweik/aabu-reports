/** GET /report/stats (any logged-in user), the numbers of the maintenance stats cards */
export interface MaintenanceStatsDTO {
  /** all reports that are fixed */
  completedReportsCount: number;
  /** fixed reports out of the reports sent this year, 0 to 100 */
  completionRatePercent: number;
  /** reports that are in progress right now */
  inProgressReportsCount: number;
  /** average hours between sending a report and fixing it, 0 when nothing is fixed yet */
  averageResponseTimeHours: number;
  /** the location with the most in-progress reports, null when none is in progress */
  activeCrewLocation: string | null;
}
