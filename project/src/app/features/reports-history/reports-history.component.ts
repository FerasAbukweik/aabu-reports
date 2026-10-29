import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ReportFilterDTO } from '../../core/DTOs/report/report-filter-dto';
import { ReportCategory } from '../../core/enums/report-category.enum';
import { ReportSeverity } from '../../core/enums/report-severity.enum';
import { ReportState } from '../../core/enums/report-state.enum';
import { Status } from '../../core/enums/status.enum';
import { MineReportsService } from '../../core/services/api-services/mine-reports.service';
import { parseEnum } from '../../core/utils/enum-utils';
import { locationStatusLabel, locationStatusTone } from '../../core/utils/location-util';
import {
  REPORT_CATEGORIES,
  reportCategoryIcon,
  reportCategoryLabel,
} from '../../core/utils/report-category-utils';
import { reportSeverityLabel, reportSeverityTone } from '../../core/utils/report-severity-utils';
import { googleMapsUrl } from '../../core/utils/map.utils';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { TableColumn } from '../../shared/components/data-table/data-table.model';
import { TableCellDirective } from '../../shared/components/data-table/table-cell.directive';
import { SearchFilterBarComponent } from '../../shared/components/search-filter-bar/search-filter-bar.component';
import {
  FilterConfig,
  FilterValues,
} from '../../shared/components/search-filter-bar/search-filter-bar.model';
import { StarRatingComponent } from '../../shared/components/star-rating/star-rating.component';
import { StatCardsComponent } from '../../shared/components/stat-card/stat-cards.component';
import { StatCard } from '../../shared/components/stat-card/stat-card.model';
import { ReportDetailsModalComponent } from './components/report-details-modal/report-details-modal.component';
import { ReportRatingModalComponent } from './components/report-rating-modal/report-rating-modal.component';
import { ReportRow } from './reports-history.model';

/** the states a report can be in (a report is never "normal", that one is only for locations without reports) */
const REPORT_STATES: readonly ReportState[] = [
  ReportState.Pending,
  ReportState.InProgress,
  ReportState.Fixed,
  ReportState.NeedsFollowUp,
];

const COLUMNS: readonly TableColumn[] = [
  { key: 'id', header: 'رقم البلاغ', nowrap: true },
  { key: 'date', header: 'تاريخ البلاغ', nowrap: true },
  { key: 'subject', header: 'موضوع ونوع المشكلة', minWidth: '15rem' },
  { key: 'location', header: 'موقع البلاغ', nowrap: true },
  { key: 'state', header: 'حالة البلاغ', nowrap: true },
  { key: 'rating', header: 'التقييم', align: 'center', nowrap: true },
  { key: 'actions', header: 'الإجراءات', align: 'center', nowrap: true },
];

const FILTERS: readonly FilterConfig[] = [
  {
    key: 'state',
    label: 'تصفية حسب الحالة',
    icon: 'tune',
    allLabel: 'جميع الحالات',
    options: REPORT_STATES.map((state) => ({
      value: String(state),
      label: locationStatusLabel[state],
    })),
  },
  {
    key: 'category',
    label: 'تصفية حسب التصنيف',
    icon: 'category',
    allLabel: 'كل التصنيفات',
    options: REPORT_CATEGORIES.map((category) => ({
      value: String(category),
      label: reportCategoryLabel[category],
    })),
  },
];

/** the search text and the picked dropdown values -> the filters of the api (an empty value is "all" and is left out) */
function toFilter(query: string, { state, category }: FilterValues): ReportFilterDTO {
  return {
    search: query.trim() || undefined,
    state: state ? (Number(state) as ReportState) : undefined,
    category: category ? (Number(category) as ReportCategory) : undefined,
  };
}

/** "3f2a9c1e-..." -> "#3F2A9C1E" */
function toShortId(id: string): string {
  return '#' + id.replace(/-/g, '').slice(0, 8).toUpperCase();
}

@Component({
  selector: 'app-reports-history',
  imports: [
    DatePipe,
    RouterLink,
    StatCardsComponent,
    SearchFilterBarComponent,
    DataTableComponent,
    TableCellDirective,
    StarRatingComponent,
    ReportDetailsModalComponent,
    ReportRatingModalComponent,
  ],
  templateUrl: './reports-history.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportsHistoryComponent {
  private readonly mineReports = inject(MineReportsService);
  private readonly destroyRef = inject(DestroyRef);

  // protected: table setup
  protected readonly columns = COLUMNS;
  protected readonly filters = FILTERS;
  protected readonly rowKey = (row: ReportRow) => row.report.id;
  /** the rows that wait for the user's rating stand out */
  protected readonly rowClass = (row: ReportRow) => (row.canRate ? 'bg-accent-light/40' : '');

  // protected: template helpers
  protected readonly ReportState = ReportState;
  protected readonly ReportSeverity = ReportSeverity;
  protected readonly stateLabel = locationStatusLabel;
  protected readonly stateTone = locationStatusTone;
  protected readonly categoryLabel = reportCategoryLabel;
  protected readonly categoryIcon = reportCategoryIcon;
  protected readonly severityLabel = reportSeverityLabel;
  protected readonly severityTone = reportSeverityTone;
  protected readonly googleMapsUrl = googleMapsUrl;

  // protected: paging of the table
  /** the page that is on the screen, the first page is 1 (the service only caches the pages) */
  protected readonly page = signal(1);
  /** the page the last request asked for (it can be different from `page` if it failed), the retry asks for it again */
  private requestedPage = 1;
  protected readonly isLoading = this.mineReports.isLoading;
  protected readonly hasLoadFailed = this.mineReports.hasLoadFailed;

  // filters
  protected readonly query = signal('');
  /** what the search box and the dropdowns show right now (not sent to the api until the apply button is pressed) */
  protected readonly filterValues = signal<FilterValues>({});
  /** the filters the table is loaded with */
  private readonly appliedFilter = signal<ReportFilterDTO>({});

  private readonly hasAppliedFilter = computed(() => {
    const { search, state, category } = this.appliedFilter();

    return search !== undefined || state !== undefined || category !== undefined;
  });

  /** the search box or the dropdowns show other filters than the table is loaded with: the apply button is enabled */
  protected readonly hasUnappliedChanges = computed(() => {
    const picked = toFilter(this.query(), this.filterValues());
    const applied = this.appliedFilter();

    return (
      picked.search !== applied.search ||
      picked.state !== applied.state ||
      picked.category !== applied.category
    );
  });

  // dialogs
  protected readonly detailsRow = signal<ReportRow | null>(null);
  protected readonly ratingRow = signal<ReportRow | null>(null);

  constructor() {
    // no filters and no cached pages: they may be old (a report was sent from the map, or its state changed, since the last visit)
    this.mineReports.setFilter({});

    // the history stats cards (the numbers stay empty until the request is done)
    this.mineReports
      .loadStats()
      .pipe(takeUntilDestroyed())
      // MineReportsService already shows the error toast, nothing else to do on failure
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
    if (stats && !this.hasAppliedFilter()) return Math.max(1, Math.ceil(stats.totalReportsCount / this.mineReports.pageSize));

    return this.page() + (this.mineReports.reports().length === this.mineReports.pageSize ? 1 : 0);
  });

  /** every report of the page on the screen */
  private readonly allRows = computed<ReportRow[]>(() => {
    return this.mineReports.reports().map((report) => {
      const state = parseEnum(ReportState, report.state, ReportState.Normal);
      const rate = report.rate ?? null;

      return {
        report,
        state,
        category: parseEnum(ReportCategory, report.category, ReportCategory.Other),
        severity: parseEnum(ReportSeverity, report.severity, ReportSeverity.Low),
        rate,
        shortId: toShortId(report.id),
        canRate: state === ReportState.Fixed && rate === null,
      };
    });
  });

  /** the reports of the page on the screen (the search and the filters are applied by the api) */
  protected readonly rows = this.allRows;

  protected readonly resultsLabel = computed(() => `${this.allRows().length}`);

  protected readonly footerLabel = computed(() => {
    const onPage = this.allRows().length;
    if (onPage === 0) return '';

    const from = (this.page() - 1) * this.mineReports.pageSize + 1;
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
  /** the history stats live in MineReportsService, the maintenance stats of the map page are not used here */
  private readonly stats = this.mineReports.stats;

  protected readonly statCards = computed<StatCard[]>(() => {
    const s = this.stats();
    const dash = '—';
    const completionRate = s && s.totalReportsCount > 0 ? Math.round((s.completedReportsCount / s.totalReportsCount) * 100) : null;

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
        note: !s ? undefined : s.inProgressReportsCount > 0 ? 'فرق الصيانة تعمل عليها الآن' : 'لا توجد أعمال جارية حاليًا',
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

    this.mineReports
      .loadPage(page)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        // the page is on the screen now (from the cache or from the backend)
        next: () => this.page.set(page),
        // MineReportsService already shows the error toast
        error: () => {},
      });
  }

  /** Loads the table again from the first page with the search text and the filters of the dropdowns (the service forgets the cached pages). */
  protected applyFilters(): void {
    const filter = toFilter(this.query(), this.filterValues());

    this.appliedFilter.set(filter);
    this.mineReports.setFilter(filter);
    this.goToPage(1);
  }

  /** The reset button emptied the search box and the dropdowns: the table follows, unless it already shows everything. */
  protected onFiltersReset(): void {
    if (this.hasUnappliedChanges()) this.applyFilters();
  }

  protected retry(): void {
    this.goToPage(this.requestedPage);
  }

  protected openDetails(row: ReportRow): void {
    this.detailsRow.set(row);
  }

  protected openRating(row: ReportRow): void {
    this.detailsRow.set(null);
    this.ratingRow.set(row);
  }

  /** the dialogs hold a row, so after a rating the row is looked up again to show the new state */
  protected readonly liveDetailsRow = computed(() => this.findRow(this.detailsRow()));
  protected readonly liveRatingRow = computed(() => this.findRow(this.ratingRow()));

  private findRow(row: ReportRow | null): ReportRow | null {
    return row ? (this.allRows().find((r) => r.report.id === row.report.id) ?? row) : null;
  }
}
