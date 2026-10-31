import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ReportDTO } from '../../core/DTOs/report/report-dto';
import { ReportFilterDTO } from '../../core/DTOs/report/report-filter-dto';
import { ReportsTableService } from '../../core/services/api-services/reports-table.service';
import { HistoryStatsComponent } from './components/history-stats/history-stats.component';
import { ReportsTableComponent } from './components/reports-table/reports-table.component';
import { SearchFilterBarComponent } from './components/search-filter-bar/search-filter-bar.component';

const NO_REPORTS: readonly ReportDTO[] = [];

/**
 * The reports history: it loads the pages of the table. The filter bar owns the filters and says them on `applied`,
 * the table owns its dialogs and texts, the stats cards load themselves, so this page only knows which page is on the
 * screen and the filters it was loaded with.
 */
@Component({
  selector: 'app-reports-history',
  imports: [
    RouterLink,
    HistoryStatsComponent,
    SearchFilterBarComponent,
    ReportsTableComponent,
  ],
  templateUrl: './reports-history.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportsHistoryComponent {
  private readonly reportsTable = inject(ReportsTableService);
  private readonly destroyRef = inject(DestroyRef);

  /** the admin route (admin/history) sets `isAdmin` in its data: the table then holds everyone's reports */
  protected readonly isAdmin = inject(ActivatedRoute).snapshot.data['isAdmin'] === true;
  protected readonly pageSize = this.reportsTable.pageSize;

  // the page on the screen
  /** the page that is on the screen, the first page is 1 (the service only caches the pages, this component says which one is shown) */
  protected readonly page = signal(1);
  /** the filters the page on the screen was loaded with */
  private readonly filter = signal<ReportFilterDTO>({});
  /** what the last request asked for (it can be different from the screen if it failed), the retry asks for it again */
  private requested = { page: 1, filter: {} as ReportFilterDTO };

  protected readonly isLoading = this.reportsTable.isLoading;
  /** the last page failed to load: the user retries by hand */
  protected readonly hasLoadFailed = signal(false);

  /** the reports of the page on the screen: read from the cache of the service (empty while it is not loaded) */
  protected readonly reports = computed(
    () => this.reportsTable.cachedPage(this.page(), this.isAdmin, this.filter()) ?? NO_REPORTS,
  );

  protected readonly isFiltered = computed(() => Object.values(this.filter()).some((value) => value !== undefined));

  /** the total of the stats counts every report, so it is only right while no filter is applied */
  private readonly stats = this.isAdmin ? this.reportsTable.allStats : this.reportsTable.myStats;
  protected readonly total = computed(() => (this.isFiltered() ? undefined : this.stats()?.totalReportsCount));

  // ---------- actions ----------
  /** the filter bar said new filters (it also says them once when the page opens): the table starts again from the first page */
  protected onFilterApplied(filter: ReportFilterDTO): void {
    this.load(1, filter);
  }

  protected goToPage(page: number): void {
    this.load(page, this.filter());
  }

  protected retry(): void {
    this.load(this.requested.page, this.requested.filter);
  }

  /**
   * A report was deleted: the service dropped the cached pages (the pages after it moved by one), so the page on the screen
   * is loaded again. If it was the last row of the last page, the previous page is shown instead.
   */
  protected onReportDeleted(): void {
    const page = this.page();
    const filter = this.filter();

    this.load(page, filter, (reports) => {
      if (reports.length === 0 && page > 1) this.load(page - 1, filter);
    });
  }

  // private
  private load(page: number, filter: ReportFilterDTO, onLoaded?: (reports: readonly ReportDTO[]) => void): void {
    this.requested = { page, filter };
    this.hasLoadFailed.set(false);

    this.reportsTable
      .loadPage(page, this.isAdmin, filter)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        // the page is on the screen now (from the cache or from the backend)
        next: (reports) => {
          this.page.set(page);
          this.filter.set(filter);
          onLoaded?.(reports);
        },
        // ReportsTableService already shows the error toast, the retry button asks for the page again
        error: () => this.hasLoadFailed.set(true),
      });
  }
}
