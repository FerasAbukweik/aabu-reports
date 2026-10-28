import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { defer, EMPTY, finalize, map, Observable, tap } from 'rxjs';
import { CUSTOM_PIN_ID } from '../../constants/google-maps';
import { LocationDTO } from '../../DTOs/location/location-dto';
import { ReportCreateDTO } from '../../DTOs/report/report-create-dto';
import { ReportCreateResponseDTO } from '../../DTOs/report/report-create-response-dto';
import { ReportDTO } from '../../DTOs/report/report-dto';
import { ReportState } from '../../enums/buildings-status';
import { toastError } from '../../utils/error-toast';
import { ReportApiService } from '../api/report-api.service';
import { ToastService } from '../local/toast.service';
import { LocationService } from './location.service';
import { MineReportsService } from './mine-reports.service';

/** how many reports of a location are loaded each time */
const LOCATION_REPORTS_PAGE_SIZE = 10;

@Injectable({ providedIn: 'root' })
export class ReportsService {
  // DI
  private reportApi = inject(ReportApiService);
  private locationService = inject(LocationService);
  private mineReports = inject(MineReportsService);
  private toast = inject(ToastService);

  // state

  /** the loaded reports of every location that was opened, the key is the location id */
  private readonly _locationReports = signal<Record<string, readonly ReportDTO[]>>({});
  public readonly locationReports = this._locationReports.asReadonly();

  /** a page of the location is loading (the first one or the next ones), the key is the location id */
  private readonly _isLocationReportsLoading = signal<Record<string, boolean>>({});
  public readonly isLocationReportsLoading = this._isLocationReportsLoading.asReadonly();


  // methods

  /**
   * Whether the location has reports that are not loaded yet: the loaded ones are fewer than
   * its `reportsCount` (the total the api sends with the location). False for a location with no
   * reports, so nothing is requested for it.
   */
  public isMoreLocationReportsAvailable(locationId: string): boolean {
    const total = this.locationService.locations().find((l) => l.id === locationId)?.reportsCount ?? 0;
    const loaded = this._locationReports()[locationId]?.length ?? 0;

    return loaded < total;
  }

  /**
   * Creates a report for the selected place.
   * A listed building / pin goes to POST /report (by location id),
   * the pin the user dropped on the map goes to POST /report/coordinates.
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
      toastError(this.toast, 'تعذر إرسال البلاغ', (e) => this.errorMessage(e, 'create')),
      tap(({ report, location: saved }) => {
        this.locationService.upsert(saved);
        // the new report goes on top of the list of its location. A location that was never loaded
        // starts its list with it (`?? []`). `taken` is the length of the list, so the next page moves with it
        this._locationReports.update((all) => ({ ...all, [saved.id]: [report, ...(all[saved.id] ?? [])] }));
      }),
    );
  }

  /**
   * Loads the next page of the reports of one location (GET /report/location) and adds it to
   * the end of that location's list in `locationReports`. Every location keeps its own list, page
   * and flag, so the first call for a location loads its first page and going back to a location
   * keeps what was loaded.
   * Does nothing when that location has no more, or a page of it is already loading.
   * Unsubscribing (switchMap) cancels the request.
   * On failure it shows an error toast and rethrows the error.
   */
  public loadMoreLocationReports(locationId: string): Observable<readonly ReportDTO[]> {
    // defer: the state is read when the request is subscribed, not when this method is called
    return defer(() => {
      if (!this.isMoreLocationReportsAvailable(locationId) || this._isLocationReportsLoading()[locationId]) {
        return EMPTY;
      }

      this.setLocationReportsLoading(locationId, true);

      // the reports already loaded of this location are the ones taken
      const taken = this._locationReports()[locationId]?.length ?? 0;

      return this.reportApi.getByLocation(locationId, { taken, sectionSize: LOCATION_REPORTS_PAGE_SIZE }).pipe(
        tap((reports) => {
          this._locationReports.update((all) => ({
            ...all,
            [locationId]: [...(all[locationId] ?? []), ...reports],
          }));
        }),
        finalize(() => this.setLocationReportsLoading(locationId, false)),
        toastError(this.toast, 'تعذر تحميل بلاغات الموقع', (e) => this.errorMessage(e, 'load')),
      );
    });
  }

  /** Gets one report (GET /report/{id}). */
  public getById(id: string): Observable<ReportDTO> {
    return this.reportApi
      .getById(id)
      .pipe(toastError(this.toast, 'تعذر تحميل البلاغ', (e) => this.errorMessage(e, 'load')));
  }

  /** Changes the state of a report (PUT /report/state, admin only). */
  public updateState(id: string, state: ReportState): Observable<ReportDTO> {
    return this.reportApi.updateState({ id, state }).pipe(
      tap((updated) => this.mineReports.replace(updated)),
      toastError(this.toast, 'تعذر تحديث حالة البلاغ', (e) => this.errorMessage(e, 'update')),
    );
  }

  /** Rates a fixed report (PUT /report/rate). */
  public rate(id: string, rate: number): Observable<ReportDTO> {
    return this.reportApi.rate({ id, rate }).pipe(
      tap((updated) => this.mineReports.replace(updated)),
      toastError(this.toast, 'تعذر إرسال التقييم', (e) => this.errorMessage(e, 'rate')),
    );
  }

  /** Deletes a report (DELETE /report/{id}, admin only). */
  public delete(id: string): Observable<void> {
    return this.reportApi.delete(id).pipe(
      tap(() => this.mineReports.remove(id)),
      toastError(this.toast, 'تعذر حذف البلاغ', (e) => this.errorMessage(e, 'delete')),
      map(() => undefined),
    );
  }

  // private
  private setLocationReportsLoading(locationId: string, isLoading: boolean): void {
    this._isLocationReportsLoading.update((all) => ({ ...all, [locationId]: isLoading }));
  }

  private errorMessage(
    error: HttpErrorResponse,
    action: 'load' | 'create' | 'update' | 'rate' | 'delete',
  ): string {
    switch (error.status) {
      case 0:
        return 'تعذّر الاتصال بالخادم، تحقق من اتصالك بالإنترنت وحاول مرة أخرى';
      case 400:
        return action === 'rate'
          ? 'لا يمكن تقييم البلاغ إلا بعد إنجاز الصيانة'
          : 'تحقق من بيانات البلاغ ثم حاول مرة أخرى';
      case 401:
      case 403:
        return action === 'load' || action === 'create' || action === 'rate'
          ? 'انتهت الجلسة أو ليس لديك صلاحية، سجّل الدخول مرة أخرى'
          : 'ليس لديك صلاحية لتنفيذ هذا الإجراء';
      case 404:
        return 'البلاغ أو الموقع غير موجود';
      default:
        return 'حدث خطأ غير متوقع، حاول مرة أخرى لاحقًا';
    }
  }
}
