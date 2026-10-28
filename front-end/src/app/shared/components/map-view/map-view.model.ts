import { LocationDTO } from "../../../core/DTOs/location/location-dto";
import { MapCoordinates } from "../../../core/interfaces/map-Coordinates";

export type LocateResult =
  | { status: 'found'; coordinates: MapCoordinates; nearest: LocationDTO | null }
  | { status: 'outside' | 'unsupported' | 'failed' };
