import { ReportDTO } from "../DTOs/report/report-dto";
import { ReportCategory } from "../enums/report-category.enum";
import { ReportSeverity } from "../enums/report-severity.enum";
import { ReportState } from "../enums/report-state.enum";

/** A row of the reports table (the report of the current user) with what the table needs next to it */
export interface TableReportRow {
  report: ReportDTO;
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