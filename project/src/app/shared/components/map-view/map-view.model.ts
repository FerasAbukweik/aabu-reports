/* eslint-disable @typescript-eslint/no-explicit-any */
import { LocationDTO } from '../../../core/DTOs/location/location-dto';
import { MapCoordinates } from '../../../core/interfaces/map-coordinates';

export type LocateResult =
  | { status: 'found'; coordinates: MapCoordinates; nearest: LocationDTO | null }
  | { status: 'outside' | 'unsupported' | 'failed' };

/**
 * What the app uses of the Google Maps API.
 * The types are `any` on purpose: install `@types/google.maps` if you want full typings.
 */
export interface GoogleMapsApi {
  Map: any;
  AdvancedMarkerElement: any;
  PinElement: any;
  event: any;
}

/** the minimal part of google.maps.Map that MapViewService needs */
export interface FocusableMap {
  panTo(target: MapCoordinates): void;
  setZoom(zoom: number): void;
  getZoom(): number | undefined;
}
