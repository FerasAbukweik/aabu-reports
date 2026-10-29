import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { defer, EMPTY, finalize, Observable, of, tap } from 'rxjs';
import { ReportDTO } from '../../DTOs/report/report-dto';
import { ReportFilterDTO } from '../../DTOs/report/report-filter-dto';
import { ReportHistoryStatsDTO } from '../../DTOs/report/report-history-stats-dto';
import { ReportTableDTO } from '../../DTOs/report/report-table-dto';
import { ReportState } from '../../enums/report-state.enum';
import { defaultErrorReason, toastError } from '../../utils/error-toast';
import { ReportApiService } from '../api/report-api.service';
import { ToastService } from '../local/toast.service';

/** how many reports of the current user one page of the history table holds */
const MINE_REPORTS_PAGE_SIZE = 10;

/** The reports table of the current user (one page at a time, pages are cached) and the numbers of the history page stats cards. */
@Injectable({ providedIn: 'root' })
export class MineReportsService {
  // DI
  private reportApi = inject(ReportApiService);
  private toast = inject(ToastService);

  readonly pageSize = MINE_REPORTS_PAGE_SIZE;

  // state

  /** the pages that were already loaded, the key is the page number (the first page is 1) */
  private readonly pages = new Map<number, readonly ReportTableDTO[]>();

  /** the filters the pages are loaded with. The cached pages belong to them, so they change only through `setFilter` */
  private filter: ReportFilterDTO = {};

  /** the rows of the page that is on the screen */
  private readonly _reports = signal<readonly ReportTableDTO[]>([]);
  public readonly reports = this._reports.asReadonly();

  /** a page is loading (the one on the screen stays until the new one arrives) */
  private readonly _isLoading = signal(false);
  public readonly isLoading = this._isLoading.asReadonly();

  /** the last page failed to load: the user retries by hand */
  private readonly _hasLoadFailed = signal(false);
  public readonly hasLoadFailed = this._hasLoadFailed.asReadonly();

  /** null until the first load succeeds */
  private readonly _stats = signal<ReportHistoryStatsDTO | null>(null);
  public readonly stats = this._stats.asReadonly();

  // methods

  /**
   * Shows one page of the reports table instead of the old one: the page comes from the cache if it was
   * loaded before, otherwise from the backend (GET /report/table) and it is cached.
   * A request is ignored while another page is loading.
   * On failure the old page stays, it shows an error toast and rethrows the error.
   */
  public loadPage(page: number): Observable<readonly ReportTableDTO[]> {
    // defer: the state is set when the request is subscribed, not when this method is called
    return defer(() => {
      const cached = this.pages.get(page);

      if (cached) {
        this._hasLoadFailed.set(false);
        this._reports.set(cached);
        return of(cached);
      }

      if (this._isLoading()) return EMPTY;

      this._hasLoadFailed.set(false);
      this._isLoading.set(true);

      return this.reportApi.getTable({ taken: (page - 1) * this.pageSize, sectionSize: this.pageSize }, this.filter).pipe(
        tap((reports) => {
          this.pages.set(page, reports);
          this._reports.set(reports);
        }),
        finalize(() => this._isLoading.set(false)),
        tap({ error: () => this._hasLoadFailed.set(true) }),
        toastError(this.toast, 'تعذر تحميل البلاغات', (e) => this.errorMessage(e)),
      );
    });
  }

  /**
   * Sets the filters of the table and forgets the cached pages (they were loaded with the old filters),
   * so the next `loadPage` asks the backend. Also used with `{}` when the history page opens, the cached pages may be old.
   */
  public setFilter(filter: ReportFilterDTO): void {
    this.filter = filter;
    this.pages.clear();
  }

  /**
   * Loads the numbers of the history stats cards (GET /report/mine/stats) into `stats`.
   * On failure it shows an error toast and rethrows the error.
   */
  public loadStats(): Observable<ReportHistoryStatsDTO> {
    return this.reportApi.getMineStats().pipe(
      tap((stats) => this._stats.set(stats)),
      toastError(this.toast, 'تعذر تحميل إحصائيات البلاغات', (e) => this.errorMessage(e)),
    );
  }

  /** Reloads the stats in the background after a report changed. No toast on failure, the old numbers stay. */
  public refreshStats(): void {
    this.reportApi.getMineStats().subscribe({
      next: (stats) => this._stats.set(stats),
      error: () => {},
    });
  }

  /**
   * Updates a report in the cached pages and on the screen (after its state changed or it was rated) and refreshes the stats.
   * @param rate the new rate when the report was just rated (the api does not send it back), otherwise the old rate is kept
   *             while the report is still fixed, and cleared when it is not
   */
  public replace(updated: ReportDTO, rate?: number): void {
    const merge = (row: ReportTableDTO): ReportTableDTO =>
      row.id !== updated.id
        ? row
        : {
            ...row,
            reportDate: updated.reportDate,
            category: updated.category,
            state: updated.state,
            severity: updated.severity,
            description: updated.description,
            rate: updated.state === ReportState.Fixed ? (rate ?? row.rate) : null,
          };

    for (const [page, rows] of this.pages) {
      if (rows.some((r) => r.id === updated.id)) this.pages.set(page, rows.map(merge));
    }

    this._reports.update((rows) => rows.map(merge));
    this.refreshStats();
  }

  /** Rates a fixed report (PUT /report/rate) and updates the table with the new rate. */
  public rate(id: string, rate: number): Observable<ReportDTO> {
    return this.reportApi.rate({ id, rate }).pipe(
      tap((updated) => this.replace(updated, rate)),
      toastError(this.toast, 'تعذر إرسال التقييم', (e) => this.rateErrorMessage(e)),
    );
  }

  /** Removes a report from the screen and refreshes the stats. The cached pages are dropped, the pages after it moved by one. */
  public remove(id: string): void {
    const onScreen = this._reports().some((r) => r.id === id);
    const cached = [...this.pages.values()].some((rows) => rows.some((r) => r.id === id));
    if (!onScreen && !cached) return;

    this.pages.clear();
    this._reports.update((rows) => rows.filter((r) => r.id !== id));
    this.refreshStats();
  }

  /** Back to the initial state (e.g. on logout). */
  public reset(): void {
    this.setFilter({});
    this._reports.set([]);
    this._stats.set(null);
    this._hasLoadFailed.set(false);
    this._isLoading.set(false);
  }

  // private
  private errorMessage(error: HttpErrorResponse): string {
    switch (error.status) {
      case 401:
      case 403:
        return 'انتهت الجلسة أو ليس لديك صلاحية، سجّل الدخول مرة أخرى';
      default:
        return defaultErrorReason(error);
    }
  }

  private rateErrorMessage(error: HttpErrorResponse): string {
    switch (error.status) {
      case 400:
        return 'لا يمكن تقييم البلاغ إلا بعد إنجاز الصيانة';
      case 404:
        return 'البلاغ أو الموقع غير موجود';
      default:
        return this.errorMessage(error);
    }
  }
}
