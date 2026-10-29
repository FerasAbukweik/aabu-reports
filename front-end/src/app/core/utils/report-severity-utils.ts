import { ReportSeverity } from '../enums/report-severity.enum';
import { Status } from '../enums/status.enum';
import { statusTone } from './status-utils';

/** The Arabic label of every report severity */
export const reportSeverityLabel: Record<ReportSeverity, string> = {
  [ReportSeverity.Low]: 'خفيف / دوري',
  [ReportSeverity.Medium]: 'متوسط',
  [ReportSeverity.High]: 'عالي / عاجل',
};

/** The tone class of every report severity (same colors as the severity picker of the report form) */
export const reportSeverityTone: Record<ReportSeverity, string> = {
  [ReportSeverity.Low]: statusTone[Status.Normal],
  [ReportSeverity.Medium]: statusTone[Status.Warning],
  [ReportSeverity.High]: statusTone[Status.Error],
};
