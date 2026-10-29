/* eslint-disable @typescript-eslint/no-explicit-any */
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  ViewEncapsulation,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { GOOGLE_MAPS_MAP_ID } from '../../../core/constants/google-maps';
import { CAMPUS_CENTER, CAMPUS_ZOOM, isInsideCampusBounds } from './map-view.bounds';
import { LocationDTO } from '../../../core/DTOs/location/location-dto';
import { nearestBuilding } from '../../../core/utils/map.utils';
import { LocationService } from '../../../core/services/api-services/location.service';
import { googleMapsAuthFailed, loadGoogleMaps } from './google-maps.loader';
import { MapViewService } from './map-view.service';
import { ThemeService } from '../../../core/services/local/theme.service';
import { ReportState } from '../../../core/enums/report-state.enum';
import { LocationTypeEnum } from '../../../core/enums/location-type.enum';
import {
  LOCATION_STATUSES,
  locationStatusLabel,
  locationStatusTone,
} from '../../../core/utils/location-util';
import { Theme } from '../../../core/interfaces/theme';
import { MapCoordinates } from '../../../core/interfaces/map-coordinates';
import { GoogleMapsApi, LocateResult } from './map-view.model';

/**
 * The interactive campus map. The selection lives in MapViewService: a click on a marker, on an
 * empty spot or on the locate button writes to it, and the map reflects whatever it holds.
 */
@Component({
  selector: 'app-map-view',
  templateUrl: './map-view.component.html',
  styleUrl: './map-view.component.css',
  // the markers are built with document.createElement and handed to Google Maps,
  // so they never get Angular's scoping attribute: the styles must be global
  encapsulation: ViewEncapsulation.None,
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex' },
})
export class MapViewComponent {
  private readonly _locationService = inject(LocationService);
  private readonly _locations = this._locationService.locations;
  private readonly _buildings = this._locationService.buildings;
  private readonly _selection = inject(MapViewService);
  private readonly _theme = inject(ThemeService).theme;

  // input
  /** show only the buildings (no pins) on the map and in the legend; the campus map shows everything */
  readonly buildingsOnly = input(false);

  // output
  readonly located = output<LocateResult>();
  /** the "details" chip was tapped (small screens: the inspector is below the map) */
  readonly detailsRequested = output<void>();

  // protected
  protected readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  protected readonly locationStatusTone = locationStatusTone;
  protected readonly locationStatusLabel = locationStatusLabel;
  protected readonly selected = this._selection.selected;

  protected readonly failed = computed(() => this.state() === 'error' || googleMapsAuthFailed());
  private readonly _shown = computed(() =>
    this.buildingsOnly() ? this._buildings() : this._locations(),
  );
  protected readonly legendItems = computed(() =>
    LOCATION_STATUSES.map((status) => ({
      status,
      count: this._shown().filter((l) => l.status === status).length,
    })),
  );

  private readonly canvas = viewChild.required<ElementRef<HTMLElement>>('canvas');
  private readonly mapReady = signal(false);
  private api?: GoogleMapsApi;
  private map?: any;
  private markers = new Map<string, { marker: any; element: HTMLElement }>();
  private pinMarker?: any;
  private destroyed = false;
  /** the theme the current map instance was created with (colorScheme can't change afterwards) */
  private mapTheme?: Theme;

  constructor() {
    afterNextRender(() => void this.init());
    inject(DestroyRef).onDestroy(() => {
      this.destroyed = true;
      this.clearMarkers();
      this.removePin();
      this._selection.unregisterMap();
      if (this.map) this.api?.event.clearInstanceListeners(this.map);
    });

    // Google only reads `colorScheme` when a map is created, so a theme change means a new map
    effect(() => {
      const theme = this._theme();
      if (!this.mapReady() || theme === this.mapTheme) return;
      untracked(() => this.recreateMap());
    });

    // the service is the source of truth; this only reflects it on the map
    effect(() => {
      const item = this._selection.selected();
      const locationId = this._selection.selectedLocationId();
      const isPin = this._selection.isDroppedPin();
      if (!this.mapReady()) return; // re-runs once the map exists

      untracked(() => {
        this.styleSelection(locationId);
        if (item && isPin) this.renderPin(item.coordinates);
        else this.removePin();
      });
    });

    // the locations come from the api: (re)draw the markers whenever the list changes
    effect(() => {
      const locations = this._shown();
      if (!this.mapReady()) return; // re-runs once the map exists

      untracked(() => {
        this.renderLocations(locations);
        this.styleSelection(this._selection.selectedLocationId());
      });
    });
  }

  // ---------- map ----------
  private async init(): Promise<void> {
    try {
      this.api = await loadGoogleMaps();
      if (this.destroyed) return;

      // a retry must not create a second map on the same element
      this.map ??= this.createMap(this._theme());

      this.mapReady.set(true);
      this.state.set('ready');
    } catch {
      if (!this.destroyed) this.state.set('error');
    }
  }

  /** rebuild the map with the new color scheme, keeping the camera where the user left it */
  private recreateMap(): void {
    if (!this.map || !this.api) return;
    const center = this.map.getCenter()?.toJSON() ?? CAMPUS_CENTER;
    const zoom = this.map.getZoom() ?? CAMPUS_ZOOM;

    this.clearMarkers();
    this.removePin();
    this.api.event.clearInstanceListeners(this.map);
    this.canvas().nativeElement.replaceChildren();

    this.map = this.createMap(this._theme(), center, zoom);

    // redraw everything on the new map
    this.renderLocations(this._shown());
    this.styleSelection(this._selection.selectedLocationId());
    const item = this._selection.selected();
    if (item && this._selection.isDroppedPin()) this.renderPin(item.coordinates);
  }

  private createMap(theme: Theme, center: MapCoordinates = CAMPUS_CENTER, zoom = CAMPUS_ZOOM): any {
    this.mapTheme = theme;
    const touch = window.matchMedia?.('(pointer: coarse)').matches ?? false;
    const map = new this.api!.Map(this.canvas().nativeElement, {
      center,
      zoom,
      mapId: GOOGLE_MAPS_MAP_ID,
      colorScheme: theme === 'dark' ? 'DARK' : 'LIGHT',
      gestureHandling: touch ? 'greedy' : 'cooperative', // desktop: Ctrl + wheel zooms
      mapTypeId: 'roadmap',
      mapTypeControl: false,
      streetViewControl: false,
      fullscreenControl: false,
      zoomControl: false,
      clickableIcons: false,
    });
    map.addListener('click', ({ latLng }: any) => {
      if (!latLng) return;
      this._selection.selectPin({
        lat: Number(latLng.lat().toFixed(6)),
        lng: Number(latLng.lng().toFixed(6)),
      });
    });
    // lets the service focus the map from anywhere (search box, inspector, ...)
    this._selection.registerMap(map);
    return map;
  }

  protected retry(): void {
    googleMapsAuthFailed.set(false);
    this.state.set('loading');
    void this.init();
  }

  // ---------- markers ----------
  private renderLocations(locations: readonly LocationDTO[]): void {
    this.clearMarkers();

    for (const location of locations) {
      if (location.type === LocationTypeEnum.Pin) this.renderListedPin(location);
      else this.renderBuilding(location);
    }
  }

  private renderBuilding(building: LocationDTO): void {
    const dot = document.createElement('span');
    dot.className = 'tone-dot';
    // only a building with an active report pulses
    if (building.status === ReportState.Pending && !this.prefersReducedMotion()) {
      dot.animate(
        [
          {
            boxShadow:
              '0 0 0 0 color-mix(in oklab, var(--tone-pin, var(--tone-main)) 60%, transparent)',
          },
          { boxShadow: '0 0 0 9px transparent' },
        ],
        { duration: 1400, iterations: Infinity },
      );
    }

    const name = document.createElement('span');
    name.textContent = building.title;

    const chip = document.createElement('div');
    chip.className = 'tone-chip';
    chip.append(dot, name);

    const tip = document.createElement('div');
    tip.className = 'map-marker-tip';

    // the status class goes on the OUTER element
    const element = document.createElement('div');
    element.className = `map-marker ${locationStatusTone[building.status]}`;
    element.append(chip, tip);

    const marker = new this.api!.AdvancedMarkerElement({
      map: this.map,
      position: { lat: building.coordinates.lat, lng: building.coordinates.lng },
      content: element,
      title: building.title,
      gmpClickable: true,
    });
    marker.addEventListener('gmp-click', () => this._selection.selectLocation(building.id));
    this.markers.set(building.id, { marker, element });
  }

  /** a pin from the list: same teardrop as the dropped pin, tinted by its status */
  private renderListedPin(pin: LocationDTO): void {
    const element = this.createPinElement();
    element.classList.add(locationStatusTone[pin.status]);

    const marker = new this.api!.AdvancedMarkerElement({
      map: this.map,
      position: { lat: pin.coordinates.lat, lng: pin.coordinates.lng },
      content: element,
      title: pin.title,
      gmpClickable: true,
    });
    marker.addEventListener('gmp-click', () => this._selection.selectLocation(pin.id));
    this.markers.set(pin.id, { marker, element });
  }

  private createPinElement(): HTMLElement {
    // teardrop head + a ground pulse; the tip of the drop sits exactly on the coordinates
    const content = document.createElement('div');
    content.className = 'map-pin';
    if (this.prefersReducedMotion()) content.classList.add('map-pin--static');
    const pulse = document.createElement('span');
    pulse.className = 'map-pin__pulse';
    const head = document.createElement('span');
    head.className = 'map-pin__head';
    const dot = document.createElement('span');
    dot.className = 'map-pin__dot';
    head.append(dot);
    content.append(pulse, head);
    return content;
  }

  private styleSelection(selectedId: string | null): void {
    for (const [id, { marker, element }] of this.markers) {
      const selected = id === selectedId;
      element.classList.toggle('selected', selected);
      marker.zIndex = selected ? 10 : 1;
    }
  }

  private renderPin(pin: MapCoordinates | null): void {
    this.removePin();
    if (!pin) return;
    const content = this.createPinElement();
    this.pinMarker = new this.api!.AdvancedMarkerElement({
      map: this.map,
      position: pin,
      content,
      title: 'الموقع المحدد',
      zIndex: 20,
    });
  }

  private clearMarkers(): void {
    this.markers.forEach(({ marker }) => (marker.map = null));
    this.markers.clear();
  }

  private removePin(): void {
    if (this.pinMarker) this.pinMarker.map = null;
    this.pinMarker = undefined;
  }

  private prefersReducedMotion(): boolean {
    return window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
  }

  // ---------- actions ----------
  protected resetZoom(): void {
    this.map?.setCenter(CAMPUS_CENTER);
    this.map?.setZoom(CAMPUS_ZOOM);
  }

  protected locateMe(): void {
    if (!('geolocation' in navigator)) {
      this.located.emit({ status: 'unsupported' });
      return;
    }
    navigator.geolocation.getCurrentPosition(
      ({ coords }) => {
        const here = { lat: coords.latitude, lng: coords.longitude };
        if (!isInsideCampusBounds(here)) {
          this.located.emit({ status: 'outside' });
          return;
        }
        this._selection.selectPin(here);
        this._selection.focusOn(here);
        this.located.emit({
          status: 'found',
          coordinates: here,
          nearest: nearestBuilding(here, this._buildings()),
        });
      },
      () => this.located.emit({ status: 'failed' }),
      { enableHighAccuracy: true, timeout: 10_000 },
    );
  }
}
