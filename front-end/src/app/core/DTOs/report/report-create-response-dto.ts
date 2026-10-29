import { LocationDTO } from '../location/location-dto';
import { ReportMapDTO } from './report-map-dto';

/** What POST /report and POST /report/coordinates return: the new report (in the short map shape), its location (status already updated) and its uploaded images */
export interface ReportCreateResponseDTO {
  report: ReportMapDTO;
  location: LocationDTO;
}
