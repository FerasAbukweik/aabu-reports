import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { finalize, Observable, tap } from 'rxjs';
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

  // methods

  /**
   * Loads the stats (GET /report/stats) into the `stats` signal.
   * On failure it shows an error toast and rethrows the error.
   */
  public load(): Observable<MaintenanceStatsDTO> {
    this._loading.set(true);

    return this.reportApi.getStats().pipe(
      tap((stats) => this._stats.set(stats)),
      finalize(() => this._loading.set(false)),
      toastError(this.toast, 'تعذر تحميل إحصائيات الصيانة', (e) => this.errorMessage(e)),
    );
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
