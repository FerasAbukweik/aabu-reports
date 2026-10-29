/** What a notification is about. The label, icon and color of each type: core/utils/notification-utils.ts */
export enum NotificationType {
  /** the report was received and waits for the maintenance crew */
  ReportReceived,
  /** the crew started working on the report */
  ReportInProgress,
  /** the report is fixed (the user can rate it) */
  ReportFixed,
  /** the report needs another look */
  ReportNeedsFollowUp,
  /** anything that is not about one report */
  System,
  /** a new message in the chat of the report */
  NewMessage,
}
