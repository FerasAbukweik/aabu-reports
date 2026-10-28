/** What is going on with a building. The colors live in BUILDING_STATUS (campus-map.data.ts). */
export enum ReportState {
  /** nothing to report */
  Normal,
  /** the last report was resolved */
  Fixed,
  /** the maintenance crew is working on it */
  InProgress,
  /** a report was submitted and nobody started working on it yet */
  Pending,
  /** resolved or not, but it needs another look */
  NeedsFollowUp,
}
