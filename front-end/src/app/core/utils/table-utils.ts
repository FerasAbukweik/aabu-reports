import { ReportDTO } from "../DTOs/report/report-dto";
import { ReportCategory } from "../enums/report-category.enum";
import { ReportSeverity } from "../enums/report-severity.enum";
import { ReportState } from "../enums/report-state.enum";
import { TableReportRow } from "../interfaces/table-row";
import { parseEnum } from "./enum-utils";

/** "3f2a9c1e-..." -> "#3F2A9C1E" */
export function toShortId(id: string): string {
  return '#' + id.replace(/-/g, '').slice(0, 8).toUpperCase();
}

/** A report from the api -> a row: the enums and the rate are read safely, the short id and `canRate` are worked out */
export function toReportTableRow(report: ReportDTO): TableReportRow {
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
