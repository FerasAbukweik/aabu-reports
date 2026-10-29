import { ReportTableDTO } from '../../core/DTOs/report/report-table-dto';
import { ReportCategory } from '../../core/enums/report-category.enum';
import { ReportSeverity } from '../../core/enums/report-severity.enum';
import { ReportState } from '../../core/enums/report-state.enum';

/** A row of the reports table (the report of the current user) with what the table needs next to it */
export interface ReportRow {
  report: ReportTableDTO;
  /** the enums and the rate of the report, read safely (the table never indexes a label map with a raw api value) */
  state: ReportState;
  category: ReportCategory;
  severity: ReportSeverity;
  rate: number | null;
  /** the short, readable number of the report (the id is a guid) */
  shortId: string;
  /** the report is fixed and the user did not rate it yet */
  canRate: boolean;
}
