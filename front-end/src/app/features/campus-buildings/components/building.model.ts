import { MapViewService } from '../../../shared/components/map-view/map-view.service';

/**
 * What the map selection holds (a saved building, or a dropped pin).
 * Derived from MapViewService so it can never drift; swap it for your own
 * Location model if you prefer an explicit import.
 */
export type SelectedLocation = NonNullable<ReturnType<MapViewService['selected']>>;

export interface BuildingFormValue {
  title: string;
  lat: number;
  lng: number;
}
