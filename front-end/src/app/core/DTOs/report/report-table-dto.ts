import { ReportDTO } from './report-dto';

export interface ReportTableDTO {
  reports: readonly ReportDTO[];
  totalCount: number;
}
