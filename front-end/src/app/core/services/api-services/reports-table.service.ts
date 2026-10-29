import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { defer, EMPTY, finalize, map, Observable, of, tap } from 'rxjs';
import { ReportDTO } from '../../DTOs/report/report-dto';
import { ReportFilterDTO } from '../../DTOs/report/report-filter-dto';
import { ReportHistoryStatsDTO } from '../../DTOs/report/report-history-stats-dto';
import { ReportState } from '../../enums/report-state.enum';
import { defaultErrorReason, toastError } from '../../utils/error-toast';
import { ReportApiService } from '../api/report-api.service';
import { ToastService } from '../local/toast.service';
import { MaintenanceStatsService } from './maintenance-stats.service';

/** how many reports one page of the reports table holds */
const REPORTS_TABLE_PAGE_SIZE = 10;

/**
 * The reports table (one page at a time, pages are cached), the numbers of its stats cards and the actions on a report
 * (get, rate, change the state, delete). By default the table holds the reports of the current user; `setAllReports(true)`
 * makes it hold everyone's reports (the backend only allows that for an admin), so the user history page and the admin page share it.
 */
@Injectable({ providedIn: 'root' })
export class ReportsTableService {
  // DI
  private reportApi = inject(ReportApiService);
  private maintenanceStats = inject(MaintenanceStatsService);
  private toast = inject(ToastService);

  readonly pageSize = REPORTS_TABLE_PAGE_SIZE;

  // state

  /** the pages that were already loaded, the key is the page number (the first page is 1) */
  private readonly pages = new Map<number, readonly ReportDTO[]>();

  /** the filters the pages are loaded with. The cached pages belong to them, so they change only through `setFilter` */
  private filter: ReportFilterDTO = {};

  /** the table holds everyone's reports (admin) instead of the current user's. Changes only through `setAllReports`, like the filter */
  private allReports = false;

  /** the rows of the page that is on the screen */
  private readonly _reports = signal<readonly ReportDTO[]>([]);
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
  public loadPage(page: number): Observable<readonly ReportDTO[]> {
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

      return this.reportApi.getTable({ taken: (page - 1) * this.pageSize, sectionSize: this.pageSize }, this.filter, this.allReports).pipe(
        tap((reports) => {
          this.pages.set(page, reports);
          this._reports.set(reports);
        }),
        finalize(() => this._isLoading.set(false)),
        tap({ error: () => this._hasLoadFailed.set(true) }),
        toastError(this.toast, 'تعذر تحميل البلاغات', (e) => this.errorMessage(e, 'load')),
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
   * Chooses whose reports the table (and its stats) hold: the current user's (the default) or everyone's (admin only).
   * Forgets the cached pages and the stats, they were loaded for the other choice, so call it before the first `loadPage` / `loadStats`.
   */
  public setAllReports(allReports: boolean): void {
    if (this.allReports === allReports) return;

    this.allReports = allReports;
    this.pages.clear();
    this._reports.set([]);
    this._stats.set(null);
  }

  /**
   * Loads the numbers of the stats cards (GET /report/table/stats) into `stats`.
   * On failure it shows an error toast and rethrows the error.
   */
  public loadStats(): Observable<ReportHistoryStatsDTO> {
    return this.reportApi.getTableStats(this.allReports).pipe(
      tap((stats) => this._stats.set(stats)),
      toastError(this.toast, 'تعذر تحميل إحصائيات البلاغات', (e) => this.errorMessage(e, 'load')),
    );
  }

  /** Reloads the stats in the background after a report changed. No toast on failure, the old numbers stay. */
  public refreshStats(): void {
    this.reportApi.getTableStats(this.allReports).subscribe({
      next: (stats) => this._stats.set(stats),
      error: () => {},
    });
  }

  /**
   * Replaces a report in the cached pages and on the screen with the one the api returned (after its state changed or it
   * was rated, the api sends the whole report back, with its rate) and refreshes the stats.
   */
  public replace(updated: ReportDTO): void {
    const merge = (row: ReportDTO): ReportDTO => (row.id === updated.id ? updated : row);

    for (const [page, rows] of this.pages) {
      if (rows.some((r) => r.id === updated.id)) this.pages.set(page, rows.map(merge));
    }

    this._reports.update((rows) => rows.map(merge));
    this.refreshStats();
  }

  /** Rates a fixed report (PUT /report/rate) and updates the table with the new rate. */
  public rate(id: string, rate: number): Observable<ReportDTO> {
    return this.reportApi.rate({ id, rate }).pipe(
      tap((updated) => this.replace(updated)),
      toastError(this.toast, 'تعذر إرسال التقييم', (e) => this.rateErrorMessage(e)),
    );
  }

  /** Gets one report (GET /report/{id}). */
  public getById(id: string): Observable<ReportDTO> {
    return this.reportApi
      .getById(id)
      .pipe(toastError(this.toast, 'تعذر تحميل البلاغ', (e) => this.errorMessage(e, 'load')));
  }

  /** Changes the state of a report (PUT /report/state, admin only) and updates the table. */
  public updateState(id: string, state: ReportState): Observable<ReportDTO> {
    return this.reportApi.updateState({ id, state }).pipe(
      tap((updated) => {
        this.replace(updated);
        this.maintenanceStats.refresh();
      }),
      toastError(this.toast, 'تعذر تحديث حالة البلاغ', (e) => this.errorMessage(e, 'update')),
    );
  }

  /** Deletes a report (DELETE /report/{id}, admin only) and removes it from the table. */
  public delete(id: string): Observable<void> {
    return this.reportApi.delete(id).pipe(
      tap(() => {
        this.remove(id);
        this.maintenanceStats.refresh();
      }),
      toastError(this.toast, 'تعذر حذف البلاغ', (e) => this.errorMessage(e, 'delete')),
      map(() => undefined),
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
    this.allReports = false;
    this.setFilter({});
    this._reports.set([]);
    this._stats.set(null);
    this._hasLoadFailed.set(false);
    this._isLoading.set(false);
  }

  // private
  private errorMessage(error: HttpErrorResponse, action: 'load' | 'update' | 'delete'): string {
    switch (error.status) {
      case 401:
      case 403:
        return action === 'load'
          ? 'انتهت الجلسة أو ليس لديك صلاحية، سجّل الدخول مرة أخرى'
          : 'ليس لديك صلاحية لتنفيذ هذا الإجراء';
      case 404:
        return action === 'load' ? defaultErrorReason(error) : 'البلاغ غير موجود';
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
        return this.errorMessage(error, 'update');
    }
  }
}
