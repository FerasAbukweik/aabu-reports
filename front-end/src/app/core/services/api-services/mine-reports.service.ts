import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { defer, EMPTY, finalize, Observable, tap } from 'rxjs';
import { ReportDTO } from '../../DTOs/report/report-dto';
import { toastError } from '../../utils/error-toast';
import { ReportApiService } from '../api/report-api.service';
import { ToastService } from '../local/toast.service';

/** how many reports of the current user are loaded each time */
const MINE_REPORTS_PAGE_SIZE = 10;

@Injectable({ providedIn: 'root' })
export class MineReportsService {
  // DI
  private reportApi = inject(ReportApiService);
  private toast = inject(ToastService);

  // state
  private readonly _reports = signal<readonly ReportDTO[]>([]);
  public readonly reports = this._reports.asReadonly();

  /** a page is loading (the first one or the next ones, the list stays on the screen) */
  private readonly _isLoading = signal(false);
  public readonly isLoading = this._isLoading.asReadonly();

  /** the api returns no total, so a full page means there may be more (a short or empty page stops it) */
  private _isMoreAvailable = true;
  public get isMoreAvailable(): boolean {
    return this._isMoreAvailable;
  }

  // methods

  /**
   * Loads the next page of the reports of the current user (GET /report/mine) and adds it to
   * the end of `reports`. The first call loads the first page.
   * Does nothing when there is no more, or a page is already loading.
   * On failure it shows an error toast and rethrows the error.
   */
  public loadMore(): Observable<readonly ReportDTO[]> {
    // defer: the state is set when the request is subscribed, not when this method is called
    return defer(() => {
      if (!this._isMoreAvailable || this._isLoading()) return EMPTY;

      this._isLoading.set(true);

      // the reports already loaded are the ones taken
      const taken = this._reports().length;

      return this.reportApi.getMine({ taken, sectionSize: MINE_REPORTS_PAGE_SIZE }).pipe(
        tap((reports) => {
          this._reports.update((list) => [...list, ...reports]);
          this._isMoreAvailable = reports.length === MINE_REPORTS_PAGE_SIZE;
        }),
        finalize(() => this._isLoading.set(false)),
        toastError(this.toast, 'تعذر تحميل البلاغات', (e) => this.errorMessage(e)),
      );
    });
  }

  /** Replaces a report in the list (after its state changed or it was rated). */
  public replace(updated: ReportDTO): void {
    this._reports.update((list) => list.map((r) => (r.id === updated.id ? updated : r)));
  }

  /** Removes a report from the list. `taken` is the length of the list, so the next page moves back with it. */
  public remove(id: string): void {
    if (!this._reports().some((r) => r.id === id)) return;

    this._reports.update((list) => list.filter((r) => r.id !== id));
  }

  /** Back to the initial state (e.g. on logout). */
  public reset(): void {
    this._reports.set([]);
    this._isMoreAvailable = true;
    this._isLoading.set(false);
  }

  // private
  private errorMessage(error: HttpErrorResponse): string {
    switch (error.status) {
      case 0:
        return 'تعذّر الاتصال بالخادم، تحقق من اتصالك بالإنترنت وحاول مرة أخرى';
      case 401:
      case 403:
        return 'انتهت الجلسة أو ليس لديك صلاحية، سجّل الدخول مرة أخرى';
      default:
        return 'حدث خطأ غير متوقع، حاول مرة أخرى لاحقًا';
    }
  }
}
