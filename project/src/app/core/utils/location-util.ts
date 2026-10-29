import { ReportState } from '../enums/report-state.enum';
import { Status } from '../enums/status.enum';
import { statusTone } from './status-utils';
import { getEnumValues } from './enum-utils';

export const locationStatusTone: Record<ReportState, string> = {
  [ReportState.Normal]: statusTone[Status.Normal],
  [ReportState.Pending]: statusTone[Status.Error],
  [ReportState.InProgress]: statusTone[Status.InProgress],
  [ReportState.Fixed]: statusTone[Status.Success],
  [ReportState.NeedsFollowUp]: statusTone[Status.Warning],
};

export const locationStatusLabel: Record<ReportState, string> = {
  [ReportState.Normal]: 'طبيعي',
  [ReportState.Pending]: 'بلاغ نشط',
  [ReportState.InProgress]: 'قيد المعالجة',
  [ReportState.Fixed]: 'تمت الصيانة ✓',
  [ReportState.NeedsFollowUp]: 'يحتاج متابعة',
};

/** All the location statuses in enum order (the order the map legend shows them) */
export const LOCATION_STATUSES: readonly ReportState[] = getEnumValues(ReportState);
