import { Injectable, computed, inject, signal } from '@angular/core';
import { CAMPUS_ZOOM, FOCUS_ZOOM } from './map-view.bounds';
import { CUSTOM_PIN_ID } from '../../../core/constants/google-maps';
import { LocationDTO } from '../../../core/DTOs/location/location-dto';
import { ReportState } from '../../../core/enums/buildings-status';
import { LocationTypeEnum } from '../../../core/enums/location-type';
import { MapCoordinates } from '../../../core/interfaces/map-Coordinates';
import { LocationService } from '../../../core/services/api-services/location.service';

/** the minimal part of google.maps.Map the service needs */
interface FocusableMap {
  panTo(target: MapCoordinates): void;
  setZoom(zoom: number): void;
  getZoom(): number | undefined;
}

/**
 * The single source of truth for the map: what is selected and where the camera points.
 * The map, the search box, the quick chips and the inspector all talk to it directly.
 */
@Injectable({ providedIn: 'root' })
export class MapViewService {
  private readonly locations = inject(LocationService).locations;
  private map?: FocusableMap;

  /** the selection resolved to a full location object */
  private readonly _selected = signal<LocationDTO | null>(null);
  readonly selected = this._selected.asReadonly();

  /** the id of the selected location from the list (building or pin); null when nothing, or the dropped pin, is selected */
  readonly selectedLocationId = computed(() => {
    const item = this._selected();
    return item && item.id !== CUSTOM_PIN_ID ? item.id : null;
  });
  /** the selection is a pin (a listed one or the one the user dropped) */
  readonly isCustomPin = computed(() => this._selected()?.type === LocationTypeEnum.Pin);
  /** the selection is the pin the user dropped on an empty spot (it is not in the list) */
  readonly isDroppedPin = computed(() => this._selected()?.id === CUSTOM_PIN_ID);

  // ---------- map registration ----------
  registerMap(map: FocusableMap): void {
    this.map = map;
  }

  unregisterMap(): void {
    this.map = undefined;
  }

  // ---------- selection ----------
  selectLocation(id: string): void {
    this._selected.set(this.locations().find((b) => b.id === id) ?? null);
  }

  selectPin(coordinates: MapCoordinates): void {
    this._selected.set({
      id: CUSTOM_PIN_ID,
      title: 'موقع مخصص على الخريطة',
      coordinates: coordinates,
      status: ReportState.Normal,
      type: LocationTypeEnum.Pin,
      reportsCount: 0,
    });
  }

  clear(): void {
    this._selected.set(null);
  }

  // ---------- focus ----------
  /** pan to the given coordinates and zoom in (never zooms out) */
  focusOn(target: MapCoordinates): void {
    if (!this.map) return;
    this.map.panTo({ lat: target.lat, lng: target.lng });
    this.map.setZoom(Math.max(this.map.getZoom() ?? CAMPUS_ZOOM, FOCUS_ZOOM));
  }

  /** focus on the currently selected place (building or custom pin), if any */
  focusSelected(): void {
    const target = this._selected()?.coordinates;
    if (target) this.focusOn(target);
  }

  /** select a building and bring it into view (used by the search box and the quick chips) */
  selectAndFocusLocation(id: string): void {
    this.selectLocation(id);
    this.focusSelected();
  }
}
