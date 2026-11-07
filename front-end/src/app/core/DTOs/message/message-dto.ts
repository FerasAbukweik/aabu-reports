/**
 * One message of the chat of a report.
 * The same shape comes from GET /message (the old pages) and from the hub's "ReceiveMessage" (the live ones).
 */
export interface MessageDTO {
  id: string;
  senderId: string;
  /** the full name of the sender */
  senderName: string;
  reportId: string;
  /** ISO date string (UTC) */
  sentAt: string;
  content: string;
}
