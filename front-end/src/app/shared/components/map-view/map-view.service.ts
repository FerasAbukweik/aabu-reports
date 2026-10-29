import { Injectable, computed, inject, signal } from '@angular/core';
import { CAMPUS_ZOOM, FOCUS_ZOOM } from './map-view.bounds';
import { FocusableMap } from './map-view.model';
import { CUSTOM_PIN_ID } from '../../../core/constants/location';
import { LocationDTO } from '../../../core/DTOs/location/location-dto';
import { ReportState } from '../../../core/enums/report-state.enum';
import { LocationTypeEnum } from '../../../core/enums/location-type.enum';
import { MapCoordinates } from '../../../core/interfaces/map-coordinates';
import { LocationService } from '../../../core/services/api-services/location.service';

/** what is selected: a location of the list (by id), or the pin the user dropped on an empty spot */
type Selection = { locationId: string } | { droppedPin: MapCoordinates } | null;

/**
 * The single source of truth for the map: what is selected and where the camera points.
 * The map, the search box, the quick chips and the inspector all talk to it directly.
 */
@Injectable({ providedIn: 'root' })
export class MapViewService {
  private readonly locations = inject(LocationService).locations;
  private map?: FocusableMap;

  private readonly selection = signal<Selection>(null);

  /**
   * The selection resolved to a full location. A location of the list is looked up by its id every time,
   * so it is never old: when the list changes (a report was sent, a status changed) this follows it.
   */
  readonly selected = computed<LocationDTO | null>(() => {
    const selection = this.selection();
    if (!selection) return null;

    if ('locationId' in selection) {
      return this.locations().find((l) => l.id === selection.locationId) ?? null;
    }

    return {
      id: CUSTOM_PIN_ID,
      title: 'موقع مخصص على الخريطة',
      coordinates: selection.droppedPin,
      status: ReportState.Normal,
      type: LocationTypeEnum.Pin,
      activeReportsCount: 0,
    };
  });

  /** the id of the selected location from the list (building or pin); null when nothing, or the dropped pin, is selected */
  readonly selectedLocationId = computed(() => {
    const selection = this.selection();
    return selection && 'locationId' in selection ? selection.locationId : null;
  });

  /** the selection is the pin the user dropped on an empty spot (it is not in the list) */
  readonly isDroppedPin = computed(() => {
    const selection = this.selection();
    return !!selection && 'droppedPin' in selection;
  });

  // ---------- map registration ----------
  registerMap(map: FocusableMap): void {
    this.map = map;
  }

  unregisterMap(): void {
    this.map = undefined;
  }

  // ---------- selection ----------
  selectLocation(id: string): void {
    this.selection.set({ locationId: id });
  }

  selectPin(coordinates: MapCoordinates): void {
    this.selection.set({ droppedPin: coordinates });
  }

  clear(): void {
    this.selection.set(null);
  }

  // ---------- focus ----------
  /** pan to the given coordinates and zoom in (never zooms out) */
  focusOn(target: MapCoordinates): void {
    if (!this.map) return;
    this.map.panTo({ lat: target.lat, lng: target.lng });
    this.map.setZoom(Math.max(this.map.getZoom() ?? CAMPUS_ZOOM, FOCUS_ZOOM));
  }

  /** focus on the currently selected place (building or pin), if any */
  focusSelected(): void {
    const target = this.selected()?.coordinates;
    if (target) this.focusOn(target);
  }

  /** select a building and bring it into view (used by the search box and the quick chips) */
  selectAndFocusLocation(id: string): void {
    this.selectLocation(id);
    this.focusSelected();
  }
}
