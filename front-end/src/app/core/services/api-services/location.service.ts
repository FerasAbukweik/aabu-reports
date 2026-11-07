import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { defer, finalize, map, Observable, of, Subject, takeUntil, tap } from 'rxjs';
import { LocationCreateDTO } from '../../DTOs/location/location-create-dto';
import { LocationDTO } from '../../DTOs/location/location-dto';
import { LocationUpdateDTO } from '../../DTOs/location/location-update-dto';
import { LocationTypeEnum } from '../../enums/location-type.enum';
import { defaultErrorReason, toastError } from '../../utils/error-toast';
import { LocationApiService } from '../api/location-api.service';
import { ToastService } from '../local/toast.service';

@Injectable({ providedIn: 'root' })
export class LocationService {
  // DI
  private locationApi = inject(LocationApiService);
  private toast = inject(ToastService);

  // state
  private readonly _locations = signal<readonly LocationDTO[] | null>(null);
  public readonly locations = computed(() => this._locations() ?? []);
  public readonly buildings = computed(() =>
    this.locations().filter((l) => l.type === LocationTypeEnum.Building),
  );

  private readonly _loading = signal(false);
  public readonly loading = this._loading.asReadonly();

  private readonly reset$ = new Subject<void>();

  // methods

  /**
   * Loads the locations only if they are not loaded yet: when they are, it does nothing (no request, the
   * `locations` signal already holds them). Use `refresh` to load them again.
   * On failure it shows an error toast and rethrows the error.
   */
  public load(): Observable<readonly LocationDTO[]> {
    return defer(() => {
      const cached = this._locations();
      return cached !== null ? of(cached) : this.refresh();
    });
  }

  /**
   * Loads every location (GET /location) into the `locations` signal, even if they are already loaded.
   * The map, the search and the inspector all read from that signal.
   * On failure it shows an error toast and rethrows the error.
   */
  public refresh(): Observable<readonly LocationDTO[]> {
    return defer(() => {
      this._loading.set(true);

      return this.locationApi.getAll().pipe(
        takeUntil(this.reset$),
        tap((locations) => this._locations.set(locations)),
        finalize(() => this._loading.set(false)),
        toastError(this.toast, 'تعذر تحميل المواقع', (e) => this.errorMessage(e, 'load')),
      );
    });
  }

  /** Adds a location, or replaces it when it is already in the list (no request to the api). */
  public upsert(location: LocationDTO): void {
    this._locations.update((curr) =>
      curr === null
        ? null
        : curr.some((l) => l.id === location.id)
          ? curr.map((l) => (l.id === location.id ? location : l))
          : [location, ...curr],
    );
  }

  /**
   * Creates a location (POST /location, admin only) and adds the returned location to a loaded list.
   */
  public create(locationCreateDTO: LocationCreateDTO): Observable<LocationDTO> {
    return this.locationApi
      .create({ ...locationCreateDTO, title: locationCreateDTO.title.trim() })
      .pipe(
        takeUntil(this.reset$),
        toastError(this.toast, 'تعذر إضافة الموقع', (e) => this.errorMessage(e, 'create')),
        tap({
          next: (newL) =>
            this._locations.update((curr) => (curr === null ? null : [newL, ...curr])),
        }),
      );
  }

  /** Updates a location (PUT /location, admin only) and replaces it in the list. */
  public update(locationUpdateDTO: LocationUpdateDTO): Observable<LocationDTO> {
    return this.locationApi
      .update({ ...locationUpdateDTO, title: locationUpdateDTO.title?.trim() })
      .pipe(
        takeUntil(this.reset$),
        tap((updated) =>
          this._locations.update(
            (list) => list?.map((l) => (l.id === updated.id ? updated : l)) ?? null,
          ),
        ),
        toastError(this.toast, 'تعذر تحديث الموقع', (e) => this.errorMessage(e, 'update')),
      );
  }

  /** Deletes a location (DELETE /location/{id}, admin only) and removes it from the list. */
  public delete(id: string): Observable<void> {
    return this.locationApi.delete(id).pipe(
      takeUntil(this.reset$),
      tap(() => this._locations.update((list) => list?.filter((l) => l.id !== id) ?? null)),
      map(() => undefined),
      toastError(this.toast, 'تعذر حذف الموقع', (e) => this.errorMessage(e, 'delete')),
    );
  }

  public reset(): void {
    this.reset$.next();
    this._locations.set(null);
    this._loading.set(false);
  }

  // private
  private errorMessage(
    error: HttpErrorResponse,
    action: 'load' | 'create' | 'update' | 'delete',
  ): string {
    switch (error.status) {
      case 400:
        return 'تحقق من بيانات الموقع ثم حاول مرة أخرى';
      case 401:
      case 403:
        return action === 'load'
          ? 'انتهت الجلسة، سجّل الدخول مرة أخرى'
          : 'ليس لديك صلاحية لتنفيذ هذا الإجراء';
      case 404:
        return 'الموقع غير موجود';
      default:
        return defaultErrorReason(error);
    }
  }
}
