import { ReportState } from '../../enums/buildings-status';
import { LocationTypeEnum } from '../../enums/location-type';
import { MapCoordinates } from '../../interfaces/map-Coordinates';

/** Only `id` is required: send just the fields that changed (the api leaves the rest as they are) */
export interface LocationUpdateDTO {
  id: string;
  title?: string;
  type?: LocationTypeEnum;
  status?: ReportState;
  coordinates?: MapCoordinates;
}
