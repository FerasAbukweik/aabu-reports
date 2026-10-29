import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { defer, EMPTY, finalize, Observable, tap } from 'rxjs';
import { CUSTOM_PIN_ID } from '../../constants/location';
import { LocationDTO } from '../../DTOs/location/location-dto';
import { ReportCreateDTO } from '../../DTOs/report/report-create-dto';
import { ReportCreateResponseDTO } from '../../DTOs/report/report-create-response-dto';
import { ReportMapDTO } from '../../DTOs/report/report-map-dto';
import { defaultErrorReason, toastError } from '../../utils/error-toast';
import { ReportApiService } from '../api/report-api.service';
import { ToastService } from '../local/toast.service';
import { LocationService } from './location.service';
import { MaintenanceStatsService } from './maintenance-stats.service';

/** how many reports of a location are loaded each time */
const LOCATION_REPORTS_PAGE_SIZE = 10;

/** The reports of the map page: the reports of every location that was opened (loaded page by page) and the creation of a new report. */
@Injectable({ providedIn: 'root' })
export class MapReportsService {
  // DI
  private reportApi = inject(ReportApiService);
  private locationService = inject(LocationService);
  private maintenanceStats = inject(MaintenanceStatsService);
  private toast = inject(ToastService);

  // state

  /** the loaded reports of every location that was opened, the key is the location id */
  private readonly _locationReports = signal<Record<string, readonly ReportMapDTO[]>>({});
  public readonly locationReports = this._locationReports.asReadonly();

  /** a page of the location is loading (the first one or the next ones), the key is the location id */
  private readonly _isLocationReportsLoading = signal<Record<string, boolean>>({});
  public readonly isLocationReportsLoading = this._isLocationReportsLoading.asReadonly();

  /**
   * the last page of the location failed to load, the key is the location id. The inspector asks for pages itself
   * when its sentinel is on the screen, and the sentinel comes back after every request, so without this it would
   * ask again and again: the user retries by hand.
   */
  private readonly _locationReportsFailed = signal<Record<string, boolean>>({});
  public readonly locationReportsFailed = this._locationReportsFailed.asReadonly();

  /**
   * the api returned an empty page for the location, so there is no more, even if its `reportsCount` says there is
   * (the count can be old, e.g. a report was deleted). Without this the sentinel would ask for the empty page again and again.
   */
  private readonly _locationReportsExhausted = signal<Record<string, boolean>>({});


  // methods

  /**
   * Whether the location has reports that are not loaded yet: the loaded ones are fewer than
   * its `reportsCount` (the total the api sends with the location). False for a location with no
   * reports, so nothing is requested for it.
   */
  public isMoreLocationReportsAvailable(locationId: string): boolean {
    if (this._locationReportsExhausted()[locationId]) return false;

    const total = this.locationService.locations().find((l) => l.id === locationId)?.reportsCount ?? 0;
    const loaded = this._locationReports()[locationId]?.length ?? 0;

    return loaded < total;
  }

  /**
   * Creates a report for the selected place.
   * A listed building / pin goes to POST /report (by location id),
   * the pin the user dropped on the map goes to POST /report/coordinates.
   * The images in `reportCreateDto.images` (if any) are uploaded with the report in the same request.
   * On success the location the api returns (its status already updated, or the new location of a
   * dropped pin) replaces the one in the locations list, no extra request.
   * On failure it shows an error toast and rethrows.
   */
  public create(location: LocationDTO, reportCreateDto: ReportCreateDTO): Observable<ReportCreateResponseDTO> {
    const request$ =
      location.id === CUSTOM_PIN_ID
        ? this.reportApi.createForCoordinates({ coordinates: location.coordinates, ...reportCreateDto })
        : this.reportApi.createForLocation({ locationId: location.id, ...reportCreateDto });

    return request$.pipe(
      toastError(this.toast, 'تعذر إرسال البلاغ', (e) => this.errorMessage(e)),
      tap(({ report, location: saved }) => {
        this.locationService.upsert(saved);
        // the new report goes on top of the list of its location. A location that was never loaded
        // starts its list with it (`?? []`). `taken` is the length of the list, so the next page moves with it
        this._locationReports.update((all) => ({ ...all, [saved.id]: [report, ...(all[saved.id] ?? [])] }));
        this.maintenanceStats.refresh();
      }),
    );
  }

  /**
   * Loads the next page of the reports of one location (GET /report/location) and adds it to
   * the end of that location's list in `locationReports`. Every location keeps its own list, page
   * and flag, so the first call for a location loads its first page and going back to a location
   * keeps what was loaded.
   * Does nothing when that location has no more, or a page of it is already loading.
   * A call after a failure is a retry (the failed flag is cleared when it starts).
   * Unsubscribing cancels the request.
   * On failure it shows an error toast and rethrows the error.
   */
  public loadMoreLocationReports(locationId: string): Observable<readonly ReportMapDTO[]> {
    // defer: the state is read when the request is subscribed, not when this method is called
    return defer(() => {
      if (!this.isMoreLocationReportsAvailable(locationId) || this._isLocationReportsLoading()[locationId]) {
        return EMPTY;
      }

      this.setLocationReportsFailed(locationId, false);
      this.setLocationReportsLoading(locationId, true);

      // the reports already loaded of this location are the ones taken
      const taken = this._locationReports()[locationId]?.length ?? 0;

      return this.reportApi.getByLocation(locationId, { taken, sectionSize: LOCATION_REPORTS_PAGE_SIZE }).pipe(
        tap((reports) => {
          this._locationReports.update((all) => ({
            ...all,
            [locationId]: [...(all[locationId] ?? []), ...reports],
          }));

          if (reports.length === 0) this._locationReportsExhausted.update((all) => ({ ...all, [locationId]: true }));
        }),
        finalize(() => this.setLocationReportsLoading(locationId, false)),
        tap({ error: () => this.setLocationReportsFailed(locationId, true) }),
        toastError(this.toast, 'تعذر تحميل بلاغات الموقع', (e) => this.errorMessage(e)),
      );
    });
  }

  // private
  private setLocationReportsLoading(locationId: string, isLoading: boolean): void {
    this._isLocationReportsLoading.update((all) => ({ ...all, [locationId]: isLoading }));
  }

  private setLocationReportsFailed(locationId: string, hasFailed: boolean): void {
    this._locationReportsFailed.update((all) => ({ ...all, [locationId]: hasFailed }));
  }

  private errorMessage(error: HttpErrorResponse): string {
    switch (error.status) {
      case 400:
        return 'تحقق من بيانات البلاغ ثم حاول مرة أخرى';
      case 401:
      case 403:
        return 'انتهت الجلسة أو ليس لديك صلاحية، سجّل الدخول مرة أخرى';
      case 404:
        return 'البلاغ أو الموقع غير موجود';
      case 413:
        return 'حجم الصور كبير جدًا، قلّل عدد الصور أو حجمها';
      default:
        return defaultErrorReason(error);
    }
  }
}
