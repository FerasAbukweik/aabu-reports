import { NotificationType } from '../../enums/notification-type.enum';

export interface NotificationDTO {
  id: string;
  /** what the notification is about, decides its label, icon and color */
  type: NotificationType;
  /** the text of the notification */
  message: string;
  /** the thing it is about, e.g. the title of the location of the report */
  subject: string | null;
  /** the report it is about, null for system notifications */
  reportId: string | null;
  /** ISO date string (UTC) */
  createdAt: string;
  /** true once the user saw it */
  isRead: boolean;
}
