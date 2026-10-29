import { Status } from '../enums/status.enum';

export const statusTone: Record<Status, string> = {
  [Status.Normal]: 'tone-normal',
  [Status.Success]: 'tone-success',
  [Status.Info]: 'tone-info',
  [Status.Warning]: 'tone-warning',
  [Status.Error]: 'tone-error',
  [Status.InProgress]: 'tone-in-progress',
};


