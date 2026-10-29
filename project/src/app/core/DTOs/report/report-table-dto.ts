import { ReportState } from '../../enums/report-state.enum';
import { ReportCategory } from '../../enums/report-category.enum';
import { ReportSeverity } from '../../enums/report-severity.enum';
import { MapCoordinates } from '../../interfaces/map-coordinates';

/** GET /report/table: one row of the reports table (the reports of the current user, newest first) */
export interface ReportTableDTO {
  id: string;
  /** ISO date string (UTC) */
  reportDate: string;
  category: ReportCategory;
  state: ReportState;
  /** how the user rated the fix (1-5), null until it is rated */
  rate: number | null;
  severity: ReportSeverity;
  description: string;
  /** the name of the location the report is about */
  locationTitle: string;
  coordinates: MapCoordinates;
  /** the urls of the images attached to the report, empty if it has none */
  imageUrls: string[];
}
