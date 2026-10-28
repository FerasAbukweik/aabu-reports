import { ReportState } from '../../enums/buildings-status';

/** PUT /report/state (admin only) */
export interface ReportUpdateStateDTO {
  id: string;
  state: ReportState;
}
