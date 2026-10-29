import { Status } from '../enums/status.enum';

export interface NotificationTypeMeta {
  /** short text of the chip: what the notification is about */
  label: string;
  /** material symbol name */
  icon: string;
  /** the tone (colors) of the chip and the icon box, see statusTone */
  status: Status;
}
