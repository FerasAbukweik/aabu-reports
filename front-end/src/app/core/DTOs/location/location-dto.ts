import { ReportState } from '../../enums/buildings-status';
import { LocationTypeEnum } from '../../enums/location-type';
import { MapCoordinates } from '../../interfaces/map-Coordinates';

export interface LocationDTO {
  id: string;

  status: ReportState;
  /** buildings have a name; pins don't need one */
  title: string;

  /** where it is on Google Maps, and nothing else */
  coordinates: MapCoordinates;

  type: LocationTypeEnum;

  /** how many reports the location has in total (the lazy loaded list does not know it) */
  reportsCount: number;
}
