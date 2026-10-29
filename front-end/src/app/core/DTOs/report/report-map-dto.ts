import { ReportState } from '../../enums/report-state.enum';
import { ReportCategory } from '../../enums/report-category.enum';
import { ReportSeverity } from '../../enums/report-severity.enum';

/** GET /report/location: a report in the short shape the map needs (the reports of one location) */
export interface ReportMapDTO {
  id: string;
  /** ISO date string (UTC) */
  reportDate: string;
  state: ReportState;
  category: ReportCategory;
  severity: ReportSeverity;
  description: string;
  locationId: string;
}
