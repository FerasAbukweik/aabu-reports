import { MapCoordinates } from '../../interfaces/map-Coordinates';
import { ReportCreateDTO } from './report-create-dto';

/** POST /report/coordinates: a report for a spot the user dropped on the map */
export interface ReportCreateForCoordinatesDTO extends ReportCreateDTO {
  coordinates: MapCoordinates;
}
