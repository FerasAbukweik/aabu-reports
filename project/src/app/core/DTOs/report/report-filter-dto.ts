import { ReportCategory } from '../../enums/report-category.enum';
import { ReportSeverity } from '../../enums/report-severity.enum';
import { ReportState } from '../../enums/report-state.enum';

/**
 * Every way to filter reports, the same DTO for all the report list endpoints (table, all, mine, location).
 * A missing field means "no filter" for it, the filled ones are combined with AND.
 */
export interface ReportFilterDTO {
  state?: ReportState;
  category?: ReportCategory;
  severity?: ReportSeverity;
  /** one text for the search box: found in the report id, the location title or the description (contained, not equal) */
  search?: string;
  /** the report id contains this text (with or without the # and the dashes) */
  id?: string;
  /** the title of the report's location contains this text */
  title?: string;
  /** the description of the report contains this text */
  description?: string;
  /** rated with exactly this rate (1-5) */
  rate?: number;
  locationId?: string;
  /** ISO date string: reports sent on or after it */
  fromDate?: string;
  /** ISO date string: reports sent on or before it */
  toDate?: string;
}
