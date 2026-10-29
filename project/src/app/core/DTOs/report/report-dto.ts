import { ReportState } from '../../enums/report-state.enum';
import { ReportCategory } from '../../enums/report-category.enum';
import { ReportSeverity } from '../../enums/report-severity.enum';

export interface ReportDTO {
  id: string;
  /** ISO date string (UTC) */
  reportDate: string;
  state: ReportState;
  category: ReportCategory;
  severity: ReportSeverity;
  description: string;
  locationId: string;
}

