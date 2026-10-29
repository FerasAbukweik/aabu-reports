import { ReportCreateDTO } from './report-create-dto';

/** POST /report: a report for a building / pin that already exists in the locations list */
export interface ReportCreateForLocationDTO extends ReportCreateDTO {
  /** the building / pin the report is about */
  locationId: string;
}
