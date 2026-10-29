import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { ReportFilterDTO } from '../../core/DTOs/report/report-filter-dto';
import { ReportCategory } from '../../core/enums/report-category.enum';
import { ReportState } from '../../core/enums/report-state.enum';
import { Status } from '../../core/enums/status.enum';
import { TableReportRow } from '../../core/interfaces/table-row';
import { toReportTableRow } from '../../core/utils/table-utils';
import { ReportsTableService } from '../../core/services/api-services/reports-table.service';
import { ReportsTableComponent } from './components/reports-table/reports-table.component';
import { SearchFilterBarComponent } from './components/search-filter-bar/search-filter-bar.component';
import { FilterValues } from './components/search-filter-bar/search-filter-bar.model';
import { StatCardsComponent } from '../../shared/components/stat-card/stat-cards.component';
import { StatCard } from '../../shared/components/stat-card/stat-card.model';
import { ReportDeleteModalComponent } from './components/report-delete-modal/report-delete-modal.component';
import { ReportRatingModalComponent } from './components/report-rating-modal/report-rating-modal.component';
import { ReportStateModalComponent } from './components/report-state-modal/report-state-modal.component';
import { FILTERS } from './reports-history.data';
import { HistoryFilterForm } from './report-history.model';

/** the raw value of the filters form (an empty string is "all" / "not set") */

/** the value of the filters form -> the filters of the api (an empty value is "all" and is left out) */
function toFilter({ query, state, category, locationId }: HistoryFilterForm): ReportFilterDTO {
  return {
    search: query.trim() || undefined,
    state: state ? (Number(state) as ReportState) : undefined,
    category: category ? (Number(category) as ReportCategory) : undefined,
    locationId: locationId || undefined,
  };
}

@Component({
  selector: 'app-reports-history',
  imports: [
    RouterLink,
    StatCardsComponent,
    SearchFilterBarComponent,
    ReportsTableComponent,
    ReportRatingModalComponent,
    ReportStateModalComponent,
    ReportDeleteModalComponent,
  ],
  templateUrl: './reports-history.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportsHistoryComponent {
  private readonly fb = inject(FormBuilder);
  private readonly reportsTable = inject(ReportsTableService);
  private readonly destroyRef = inject(DestroyRef);
  /** the admin route (admin/history) sets `isAdmin` in its data: the table then holds everyone's reports */
  protected readonly isAdmin = inject(ActivatedRoute).snapshot.data['isAdmin'] === true;
  protected readonly queryParams = inject(ActivatedRoute).snapshot.queryParams;

  // protected: filters setup
  protected readonly filters = FILTERS;

  // protected: paging of the table
  /** the page that is on the screen, the first page is 1 (the service only caches the pages) */
  protected readonly page = signal(1);
  /** the page the last request asked for (it can be different from `page` if it failed), the retry asks for it again */
  private requestedPage = 1;
  protected readonly isLoading = this.reportsTable.isLoading;
  protected readonly hasLoadFailed = this.reportsTable.hasLoadFailed;

  // filters
  /**
   * what the search box and the dropdowns show right now (not sent to the api until the apply button is pressed).
   * `locationId` is the location the map page sent us here with (`?l_id=`), it is applied from the first load
   */
  protected readonly form = this.fb.nonNullable.group<HistoryFilterForm>({
    query: '',
    state: '',
    category: '',
    locationId: this.queryParams['l_id'] ?? '',
  });

  /** the form as a signal, so the computed values below and the OnPush template follow every change */
  private readonly formValue = toSignal(
    this.form.valueChanges.pipe(map(() => this.form.getRawValue())),
    { initialValue: this.form.getRawValue() },
  );

  protected readonly query = computed(() => this.formValue().query);

  /** the dropdown values in the shape the filter bar expects */
  protected readonly filterValues = computed<FilterValues>(() => {
    const { state, category, locationId } = this.formValue();

    return { state, category, locationId };
  });

  /** the filters the table is loaded with */
  private readonly appliedFilter = signal<ReportFilterDTO>(toFilter(this.form.getRawValue()));

  private readonly hasAppliedFilter = computed(() => {
    const { search, state, category, locationId } = this.appliedFilter();

    return (
      search !== undefined ||
      state !== undefined ||
      category !== undefined ||
      locationId !== undefined
    );
  });

  /** the search box, the dropdowns or the location differ from what the table is loaded with: the apply button is enabled */
  protected readonly hasUnappliedChanges = computed(() => {
    const picked = toFilter(this.formValue());
    const applied = this.appliedFilter();

    return (
      picked.search !== applied.search ||
      picked.state !== applied.state ||
      picked.category !== applied.category ||
      picked.locationId !== applied.locationId
    );
  });

  // dialog
  protected readonly ratingRow = signal<TableReportRow | null>(null);
  /** admin only: the row whose state is being changed / that is about to be deleted */
  protected readonly stateRow = signal<TableReportRow | null>(null);
  protected readonly deleteRow = signal<TableReportRow | null>(null);

  constructor() {
    // before any load: the service is shared, so it is set on the user page too (false)
    this.reportsTable.setAllReports(this.isAdmin);

    // no cached pages: they may be old (a report was sent from the map, or its state changed, since the last visit).
    // the location of the map page (if any) is already part of the applied filter
    this.reportsTable.setFilter(this.appliedFilter());

    // the history stats cards (the numbers stay empty until the request is done)
    this.reportsTable
      .loadStats()
      .pipe(takeUntilDestroyed())
      // ReportsTableService already shows the error toast, nothing else to do on failure
      .subscribe({ error: () => {} });

    // always start from the first page, the list may be old (a report was sent from the map meanwhile)
    this.goToPage(1);
  }

  // ---------- rows ----------
  /**
   * How many pages the table has. It comes from the total in the stats; while the stats are not
   * loaded (or failed) a full page means there may be one more.
   */
  protected readonly totalPages = computed(() => {
    const stats = this.stats();

    // the total of the stats counts every report, so it is only right while no filter is applied
    if (stats && !this.hasAppliedFilter())
      return Math.max(1, Math.ceil(stats.totalReportsCount / this.reportsTable.pageSize));

    return (
      this.page() + (this.reportsTable.reports().length === this.reportsTable.pageSize ? 1 : 0)
    );
  });

  /** the reports of the page on the screen (the search and the filters are applied by the api) */
  protected readonly rows = computed<TableReportRow[]>(() =>
    this.reportsTable.reports().map(toReportTableRow),
  );

  protected readonly resultsLabel = computed(() => `${this.rows().length}`);

  protected readonly footerLabel = computed(() => {
    const onPage = this.rows().length;
    if (onPage === 0) return '';

    const from = (this.page() - 1) * this.reportsTable.pageSize + 1;
    const to = from + onPage - 1;
    // the total of the stats counts every report, so it is only right while no filter is applied
    const total = this.hasAppliedFilter() ? undefined : this.stats()?.totalReportsCount;

    return total !== undefined
      ? `عرض البلاغات ${from} - ${to} من أصل ${total} بلاغاً`
      : `عرض البلاغات ${from} - ${to}`;
  });

  protected readonly emptyMessage = computed(() =>
    this.hasAppliedFilter()
      ? 'لا توجد بلاغات مطابقة للبحث أو التصفية، جرّب تغيير البحث أو اضغط إعادة الضبط'
      : 'لم تقدّم أي بلاغ بعد، ابدأ بتحديد المشكلة من خريطة الحرم',
  );

  // ---------- stats ----------
  /** the history stats live in ReportsTableService, the maintenance stats of the map page are not used here */
  private readonly stats = this.reportsTable.stats;

  protected readonly statCards = computed<StatCard[]>(() => {
    const s = this.stats();
    const dash = '—';
    const completionRate =
      s && s.totalReportsCount > 0
        ? Math.round((s.completedReportsCount / s.totalReportsCount) * 100)
        : null;

    return [
      {
        label: 'إجمالي البلاغات المسجلة',
        value: s ? String(s.totalReportsCount) : dash,
        unit: 'بلاغ',
        icon: 'folder_open',
        status: Status.Info,
        note: 'جميع البلاغات التي قدمتها',
        noteIcon: 'history',
      },
      {
        label: 'قيد المتابعة والتنفيذ',
        value: s ? String(s.inProgressReportsCount) : dash,
        unit: 'حالات نشطة',
        icon: 'engineering',
        status: Status.InProgress,
        note: !s
          ? undefined
          : s.inProgressReportsCount > 0
            ? 'فرق الصيانة تعمل عليها الآن'
            : 'لا توجد أعمال جارية حاليًا',
        notePulse: !!s && s.inProgressReportsCount > 0,
      },
      {
        label: 'تم إنجازها بنجاح',
        value: s ? String(s.completedReportsCount) : dash,
        unit: 'مكتملة ومغلقة',
        icon: 'task_alt',
        status: Status.Success,
        note: completionRate !== null ? `نسبة حل بلغت ${completionRate}%` : undefined,
        noteIcon: 'verified',
      },
      {
        label: 'بانتظار تقييمك للخدمة',
        value: s ? String(s.awaitingRatingReportsCount) : dash,
        unit: 'يتطلب رأيك',
        icon: 'reviews',
        status: Status.Warning,
        note: 'تقييم الجودة يعزز الخدمة',
        noteIcon: 'star',
      },
    ];
  });

  // ---------- actions ----------
  protected goToPage(page: number): void {
    this.requestedPage = page;

    this.reportsTable
      .loadPage(page)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        // the page is on the screen now (from the cache or from the backend)
        next: () => this.page.set(page),
        // ReportsTableService already shows the error toast
        error: () => {},
      });
  }

  /** the filter bar emitted a new search text */
  protected onQueryChange(query: string): void {
    this.form.controls.query.setValue(query);
  }

  /** the filter bar emitted new dropdown values (a missing key is "all") */
  protected onValuesChange({ state, category }: FilterValues): void {
    this.form.patchValue({ state: state ?? '', category: category ?? '' });
  }

  /** Loads the table again from the first page with the search text, the filters of the dropdowns and the location (the service forgets the cached pages). */
  protected applyFilters(): void {
    const filter = toFilter(this.form.getRawValue());

    this.appliedFilter.set(filter);
    this.reportsTable.setFilter(filter);
    this.goToPage(1);
  }

  /** The reset button emptied the search box and the dropdowns: the location of the map page is dropped too, then the table follows unless it already shows everything. */
  protected onFiltersReset(): void {
    // not `reset()`: a non-nullable control goes back to its initial value, and that is the `l_id` of the map page
    this.form.controls.locationId.setValue('');

    if (this.hasUnappliedChanges()) this.applyFilters();
  }

  protected retry(): void {
    this.goToPage(this.requestedPage);
  }

  protected openRating(row: TableReportRow): void {
    this.ratingRow.set(row);
  }

  protected openStateChange(row: TableReportRow): void {
    this.stateRow.set(row);
  }

  protected openDelete(row: TableReportRow): void {
    this.deleteRow.set(row);
  }

  /**
   * A report was deleted: the service dropped the cached pages (the pages after it moved by one), so the page on the screen
   * is loaded again. If it was the last row of the last page, the previous page is shown instead.
   */
  protected onReportDeleted(): void {
    const page = this.page();

    this.reportsTable
      .loadPage(page)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (reports) => {
          if (reports.length === 0 && page > 1) this.goToPage(page - 1);
        },
        // ReportsTableService already shows the error toast, the retry button asks for the page again
        error: () => {},
      });
  }

  /** the dialogs hold a row, so after a change the row is looked up again to show the new state */
  protected readonly liveRatingRow = computed(() => this.findRow(this.ratingRow()));
  protected readonly liveStateRow = computed(() => this.findRow(this.stateRow()));

  private findRow(row: TableReportRow | null): TableReportRow | null {
    return row ? (this.rows().find((r) => r.report.id === row.report.id) ?? row) : null;
  }
}
