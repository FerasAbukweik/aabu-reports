import { LocationDTO } from '../location/location-dto';
import { ReportDTO } from './report-dto';

/** What POST /report and POST /report/coordinates return: the new report, its location (status already updated) and its uploaded images */
export interface ReportCreateResponseDTO {
  report: ReportDTO;
  location: LocationDTO;
}
