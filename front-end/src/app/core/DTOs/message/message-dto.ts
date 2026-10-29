/**
 * One message of the chat of a report.
 * The same shape comes from GET /message (the old pages) and from the hub's "ReceiveMessage" (the live ones).
 * It has no id: two messages are the same one when `messageKey` says so.
 */
export interface MessageDTO {
  senderId: string;
  /** the full name of the sender */
  senderName: string;
  reportId: string;
  /** ISO date string (UTC) */
  sentAt: string;
  content: string;
}

/** what makes a message the same one when it comes twice (a page and the hub, or a page after a reconnect) */
export function messageKey(message: MessageDTO): string {
  return `${message.senderId}|${message.sentAt}|${message.content}`;
}
