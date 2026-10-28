import { ReportState } from '../../enums/buildings-status';
import { ReportCategory } from '../../enums/report-category.enum';
import { ReportSeverity } from '../../enums/report-severity.enum';

export interface ReportDTO {
  id: string;
  /** ISO date string (UTC) */
  reportDate: string;
  category: ReportCategory;
  severity: ReportSeverity;
  description: string;
  state: ReportState;
  /** only set once the report is fixed and the user rated it */
  rate: number | null;
  locationId: string;
}
