import { ReportState } from '../../enums/report-state.enum';
import { LocationTypeEnum } from '../../enums/location-type.enum';
import { MapCoordinates } from '../../interfaces/map-coordinates';

/** Only `id` is required: send just the fields that changed (the api leaves the rest as they are) */
export interface LocationUpdateDTO {
  id: string;
  title?: string;
  type?: LocationTypeEnum;
  status?: ReportState;
  coordinates?: MapCoordinates;
}
