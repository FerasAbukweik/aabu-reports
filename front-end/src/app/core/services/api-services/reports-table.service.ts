import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Subject, takeUntil, defer, EMPTY, finalize, map, Observable, of, tap } from 'rxjs';
import { ReportTableDTO } from '../../DTOs/report/report-table-dto';
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
 * The filters as text, to be part of a cache key: the empty ones are left out and the rest are sorted by name, so the same
 * filters always give the same text ("" for no filters).
 */
const filterKey = (filter: ReportFilterDTO) =>
  JSON.stringify(
    Object.entries(filter)
      .filter(([, value]) => value !== undefined && value !== null && value !== '')
      .sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)),
  );

/**
 * the key of a page in the cache: kind, filters and page number. `all|[]|3` is the third page of everyone's reports
 * (admin) with no filters, `mine|[["state",1]]|3` the third page of the current user's reports with the state filter.
 */
const pageKey = (page: number, isAll: boolean, filter: ReportFilterDTO) =>
  `${isAll ? 'all' : 'mine'}|${filterKey(filter)}|${page}`;

/**
 * The reports of the table, page by page, and the numbers of its stats cards. It only fetches, caches and gives: it does
 * not know which page is on the screen, whose reports the screen shows or which filters it uses, the component says it on
 * every call (`isAll` is true for everyone's reports, which the backend only allows for an admin).
 * Every page is cached, with or without filters: the filters are part of the key of the page, so the same page of
 * the same filters is not requested twice.
 * A cached page is read with `cachedPage`, it reads a signal, so a report that changed (state, rate) changes in it by itself.
 */
@Injectable({ providedIn: 'root' })
export class ReportsTableService {
  // DI
  private reportApi = inject(ReportApiService);
  private toast = inject(ToastService);
  private maintenanceStats = inject(MaintenanceStatsService);

  readonly pageSize = REPORTS_TABLE_PAGE_SIZE;

  // state

  /** the pages that were already loaded, the key is `pageKey` (the first page is 1) */
  private readonly pages = signal<ReadonlyMap<string, ReportTableDTO>>(new Map());

  /** a page is loading */
  private readonly _isLoading = signal(false);
  public readonly isLoading = this._isLoading.asReadonly();

  /** null until the first load succeeds */
  private readonly _allStats = signal<ReportHistoryStatsDTO | null>(null);
  public readonly allStats = this._allStats.asReadonly();

  private readonly _myStats = signal<ReportHistoryStatsDTO | null>(null);
  public readonly myStats = this._myStats.asReadonly();

  private readonly reset$ = new Subject<void>();

  // methods

  /**
   * The cached page (the first page is 1) of the reports with these filters, undefined when it was not loaded yet.
   * It reads a signal, so it can be used inside `computed` and templates.
   */
  public cachedPage(
    page: number,
    isAll: boolean,
    filter: ReportFilterDTO = {},
  ): readonly ReportDTO[] | undefined {
    return this.pages().get(pageKey(page, isAll, filter))?.reports;
  }

  public cachedTotal(
    page: number,
    isAll: boolean,
    filter: ReportFilterDTO = {},
  ): number | undefined {
    return this.pages().get(pageKey(page, isAll, filter))?.totalCount;
  }

  /**
   * Gives one page of the reports table: from the cache if it was loaded before with the same filters, otherwise from
   * the backend (GET /report/table) and it is cached under its filters.
   * A request is ignored while another page is loading.
   * On failure it shows an error toast and rethrows the error (the caller keeps track of the failure).
   */
  public loadPage(
    page: number,
    isAll: boolean,
    filter: ReportFilterDTO = {},
  ): Observable<readonly ReportDTO[]> {
    // defer: the state is set when the request is subscribed, not when this method is called
    return defer(() => {
      const key = pageKey(page, isAll, filter);
      const cached = this.pages().get(key);

      if (cached !== undefined) return of(cached.reports);

      if (this._isLoading()) return EMPTY;

      this._isLoading.set(true);

      return this.reportApi
        .getTable({ taken: (page - 1) * this.pageSize, sectionSize: this.pageSize }, filter, isAll)
        .pipe(
          takeUntil(this.reset$),
          tap((response) => this.pages.update((pages) => new Map(pages).set(key, response))),
          map((response) => response.reports),
          finalize(() => this._isLoading.set(false)),
          toastError(this.toast, 'تعذر تحميل البلاغات', (e) => this.errorMessage(e, 'load')),
        );
    });
  }

  /**
   * Forgets cached pages for every filter, so the next load requests current rows and totals.
   */
  public clearPages(): void {
    this.pages.set(new Map());
  }

  /**
   * Loads the numbers of the stats cards (GET /report/table/stats) into `allStats` or `myStats`.
   * If that kind was loaded before it is given as it is, nothing is requested again (`refreshStats` keeps it up to date
   * after a report changes).
   * On failure it shows an error toast and rethrows the error.
   */
  public loadStats(isAll: boolean): Observable<ReportHistoryStatsDTO> {
    // defer: the state is read when the request is subscribed, not when this method is called
    return defer(() => {
      const loaded = this.statsOf(isAll)();
      if (loaded) return of(loaded);

      return this.reportApi.getTableStats(isAll).pipe(
        takeUntil(this.reset$),
        tap((stats) => this.statsOf(isAll).set(stats)),
        toastError(this.toast, 'تعذر تحميل إحصائيات البلاغات', (e) => this.errorMessage(e, 'load')),
      );
    });
  }

  /** Reloads the stats that were loaded before, in the background, after a report changed. No toast on failure, the old numbers stay. */
  public refreshStats(): void {
    for (const isAll of [true, false]) {
      if (this.statsOf(isAll)() === null) continue;

      this.reportApi
        .getTableStats(isAll)
        .pipe(takeUntil(this.reset$))
        .subscribe({
          next: (stats) => this.statsOf(isAll).set(stats),
          error: () => {},
        });
    }
  }

  /**
   * Invalidates table pages and refreshes loaded stats after a successful report write.
   */
  public invalidate(): void {
    // State/rating filters can change membership and page boundaries, including pages without this report.
    this.clearPages();
    this.maintenanceStats.reset();
    this.refreshStats();
  }

  /** Rates a fixed report (PUT /report/rate) and updates the table with the new rate. */
  public rate(id: string, rate: number): Observable<ReportDTO> {
    return this.reportApi.rate({ id, rate }).pipe(
      takeUntil(this.reset$),
      tap(() => this.invalidate()),
      toastError(this.toast, 'تعذر إرسال التقييم', (e) => this.rateErrorMessage(e)),
    );
  }

  /** Gets one report (GET /report/{id}). */
  public getById(id: string): Observable<ReportDTO> {
    return this.reportApi.getById(id).pipe(
      takeUntil(this.reset$),
      toastError(this.toast, 'تعذر تحميل البلاغ', (e) => this.errorMessage(e, 'load')),
    );
  }

  /** Changes the state of a report (PUT /report/state, admin only) and updates the table. */
  public updateState(id: string, state: ReportState): Observable<ReportDTO> {
    return this.reportApi.updateState({ id, state }).pipe(
      takeUntil(this.reset$),
      tap(() => this.invalidate()),
      toastError(this.toast, 'تعذر تحديث حالة البلاغ', (e) => this.errorMessage(e, 'update')),
    );
  }

  /** Deletes a report (DELETE /report/{id}, admin only) and removes it from the table. */
  public delete(id: string): Observable<void> {
    return this.reportApi.delete(id).pipe(
      takeUntil(this.reset$),
      tap(() => this.invalidate()),
      toastError(this.toast, 'تعذر حذف البلاغ', (e) => this.errorMessage(e, 'delete')),
      map(() => undefined),
    );
  }

  /** Back to the initial state (e.g. on logout). */
  public reset(): void {
    this.reset$.next();
    this.clearPages();
    this._allStats.set(null);
    this._myStats.set(null);
    this._isLoading.set(false);
  }

  // private
  private statsOf(isAll: boolean) {
    return isAll ? this._allStats : this._myStats;
  }

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
