import { LocationDTO } from '../location/location-dto';
import { ReportDTO } from './report-dto';

/** What POST /report and POST /report/coordinates return: the new report and its location (status already updated) */
export interface ReportCreateResponseDTO {
  report: ReportDTO;
  location: LocationDTO;
}
