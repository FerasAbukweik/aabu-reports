import { ReportState } from '../../enums/report-state.enum';

/** PUT /report/state (admin only) */
export interface ReportUpdateStateDTO {
  id: string;
  state: ReportState;
}
