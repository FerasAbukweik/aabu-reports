import { Status } from '../enums/status.enum';

export interface ToastOptions {
  /** material symbol name, overrides the default icon of the status */
  icon?: string;
}

export interface Toast {
  id: string;
  status: Status;
  title: string;
  message: string;
  icon: string;
}
