import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Subject, takeUntil, defer, finalize, Observable, of, tap } from 'rxjs';
import { MaintenanceStatsDTO } from '../../DTOs/report/maintenance-stats-dto';
import { defaultErrorReason, toastError } from '../../utils/error-toast';
import { ReportApiService } from '../api/report-api.service';
import { ToastService } from '../local/toast.service';

@Injectable({ providedIn: 'root' })
export class MaintenanceStatsService {
  // DI
  private reportApi = inject(ReportApiService);
  private toast = inject(ToastService);

  // state

  /** null until the first load succeeds */
  private readonly _stats = signal<MaintenanceStatsDTO | null>(null);
  public readonly stats = this._stats.asReadonly();

  private readonly _loading = signal(false);
  public readonly loading = this._loading.asReadonly();

  private readonly reset$ = new Subject<void>();

  // methods

  /**
   * Loads the stats (GET /report/stats) into the `stats` signal.
   * If they were loaded before they are given as they are, nothing is requested again.
   * On failure it shows an error toast and rethrows the error.
   */
  public load(): Observable<MaintenanceStatsDTO> {
    // defer: the state is read when the request is subscribed, not when this method is called
    return defer(() => {
      const loaded = this._stats();
      if (loaded) return of(loaded);

      this._loading.set(true);

      return this.reportApi.getStats().pipe(
        takeUntil(this.reset$),
        tap((stats) => this._stats.set(stats)),
        finalize(() => this._loading.set(false)),
        toastError(this.toast, 'تعذر تحميل إحصائيات الصيانة', (e) => this.errorMessage(e)),
      );
    });
  }

  public reset(): void {
    this.reset$.next();
    this._stats.set(null);
    this._loading.set(false);
  }

  // private
  private errorMessage(error: HttpErrorResponse): string {
    switch (error.status) {
      case 401:
      case 403:
        return 'انتهت الجلسة، سجّل الدخول مرة أخرى';
      default:
        return defaultErrorReason(error);
    }
  }
}
