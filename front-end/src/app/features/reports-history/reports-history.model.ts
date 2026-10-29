import { ReportDTO } from '../../core/DTOs/report/report-dto';
import { ReportCategory } from '../../core/enums/report-category.enum';
import { ReportSeverity } from '../../core/enums/report-severity.enum';
import { ReportState } from '../../core/enums/report-state.enum';
import { parseEnum } from '../../core/utils/enum-utils';

/** A row of the reports table (the report of the current user) with what the table needs next to it */
export interface ReportRow {
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

/** "3f2a9c1e-..." -> "#3F2A9C1E" */
export function toShortId(id: string): string {
  return '#' + id.replace(/-/g, '').slice(0, 8).toUpperCase();
}

/** A report from the api -> a row: the enums and the rate are read safely, the short id and `canRate` are worked out */
export function toReportRow(report: ReportDTO): ReportRow {
  const state = parseEnum(ReportState, report.state, ReportState.Normal);
  const rate = report.rate ?? null;

  return {
    report,
    state,
    category: parseEnum(ReportCategory, report.category, ReportCategory.Other),
    severity: parseEnum(ReportSeverity, report.severity, ReportSeverity.Low),
    rate,
    shortId: toShortId(report.id),
    canRate: state === ReportState.Fixed && rate === null,
  };
}
