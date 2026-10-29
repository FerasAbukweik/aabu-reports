import { LocationTypeEnum } from '../../enums/location-type.enum';
import { MapCoordinates } from '../../interfaces/map-coordinates';

/** POST /location (admin only) */
export interface LocationCreateDTO {
  title: string;
  coordinates: MapCoordinates;
  type: LocationTypeEnum;
}
