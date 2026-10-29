import { ReportState } from '../../enums/report-state.enum';
import { LocationTypeEnum } from '../../enums/location-type.enum';
import { MapCoordinates } from '../../interfaces/map-coordinates';

export interface LocationDTO {
  id: string;

  status: ReportState;
  /** the name of the building, or a generic name for a pin */
  title: string;

  /** where it is on Google Maps, and nothing else */
  coordinates: MapCoordinates;

  type: LocationTypeEnum;

  /** how many reports the location has in total (the lazy loaded list does not know it) */
  reportsCount: number;
}
