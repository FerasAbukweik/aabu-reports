import { NotificationType } from '../enums/notification-type.enum';
import { Status } from '../enums/status.enum';
import { NotificationTypeMeta } from '../interfaces/notification-type-meta';

export const notificationTypeMeta: Record<NotificationType, NotificationTypeMeta> = {
  [NotificationType.ReportReceived]: { label: 'استلام بلاغ', icon: 'inbox', status: Status.Info },
  [NotificationType.ReportInProgress]: { label: 'قيد التنفيذ', icon: 'engineering', status: Status.Warning },
  [NotificationType.ReportFixed]: { label: 'تم الإنجاز', icon: 'task_alt', status: Status.Success },
  [NotificationType.ReportNeedsFollowUp]: { label: 'يحتاج متابعة', icon: 'flag', status: Status.Error },
  [NotificationType.System]: { label: 'إشعار عام', icon: 'campaign', status: Status.Normal },
};
