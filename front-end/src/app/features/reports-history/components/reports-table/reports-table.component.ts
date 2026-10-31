import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ReportDTO } from '../../../../core/DTOs/report/report-dto';
import { TableReportRow } from '../../../../core/interfaces/table-row';
import { ReportSeverity } from '../../../../core/enums/report-severity.enum';
import { ReportState } from '../../../../core/enums/report-state.enum';
import { locationStatusLabel, locationStatusTone } from '../../../../core/utils/location-util';
import { reportCategoryIcon, reportCategoryLabel } from '../../../../core/utils/report-category-utils';
import { reportSeverityLabel, reportSeverityTone } from '../../../../core/utils/report-severity-utils';
import { googleMapsUrl } from '../../../../core/utils/map.utils';
import { toReportTableRow } from '../../../../core/utils/table-utils';
import { StarRatingComponent } from '../../../../shared/components/star-rating/star-rating.component';
import { ReportDeleteModalComponent } from '../report-delete-modal/report-delete-modal.component';
import { ReportRatingModalComponent } from '../report-rating-modal/report-rating-modal.component';
import { ReportStateModalComponent } from '../report-state-modal/report-state-modal.component';

/**
 * The reports table of the history page: fixed columns, the rows, the page numbers, loading, retry and empty state, and the
 * dialogs of the row buttons (rate, change the state, delete). The page only gives the reports of the page on the screen.
 *
 *   <app-reports-table [reports]="reports()" [page]="page()" [pageSize]="10" [total]="total()" ... />
 */
@Component({
  selector: 'app-reports-table',
  imports: [
    DatePipe,
    RouterLink,
    StarRatingComponent,
    ReportRatingModalComponent,
    ReportStateModalComponent,
    ReportDeleteModalComponent,
  ],
  templateUrl: './reports-table.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class ReportsTableComponent {
  // input
  /** the reports of the page on the screen */
  readonly reports = input.required<readonly ReportDTO[]>();

  /** the page on the screen (the first is 1) and how many reports a page holds */
  readonly page = input(1);
  readonly pageSize = input.required<number>();
  /** how many reports there are in all, undefined when it is not known (a filter is applied): then a full page means there may be one more */
  readonly total = input<number>();
  /** a filter is applied: it only changes the text of the empty state */
  readonly isFiltered = input(false);

  /** a page is loading: the rows stay, dimmed, and the numbers are disabled */
  readonly isLoading = input(false);
  /** the last page failed to load */
  readonly hasFailed = input(false);

  /** the admin page: the actions column also shows the "change state" and "delete" buttons, and the rate button is hidden (an admin does not rate) */
  readonly isAdmin = input(false);

  // output
  /** the user picked a page number */
  readonly pageChange = output<number>();
  /** the retry button was pressed after a failed load */
  readonly retryRequested = output<void>();
  /** the admin deleted a report: the page on the screen has to be loaded again */
  readonly reportDeleted = output<void>();

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

  protected readonly rows = computed(() => this.reports().map(toReportTableRow));

  /** how many pages there are. It comes from the total; while the total is not known a full page means there may be one more */
  protected readonly totalPages = computed(() => {
    const total = this.total();

    if (total !== undefined) return Math.max(1, Math.ceil(total / this.pageSize()));

    return this.page() + (this.rows().length === this.pageSize() ? 1 : 0);
  });

  /** text on the start side of the footer, e.g. "عرض البلاغات 1 - 10" */
  protected readonly footerLabel = computed(() => {
    const onPage = this.rows().length;
    if (onPage === 0) return '';

    const from = (this.page() - 1) * this.pageSize() + 1;
    const to = from + onPage - 1;
    const total = this.total();

    return total !== undefined
      ? `عرض البلاغات ${from} - ${to} من أصل ${total} بلاغاً`
      : `عرض البلاغات ${from} - ${to}`;
  });

  protected readonly emptyMessage = computed(() =>
    this.isFiltered()
      ? 'لا توجد بلاغات مطابقة للبحث أو التصفية، جرّب تغيير البحث أو اضغط إعادة الضبط'
      : 'لم تقدّم أي بلاغ بعد، ابدأ بتحديد المشكلة من خريطة الحرم',
  );

  // the rows the dialogs are open for (null keeps the dialog closed)
  protected readonly ratingRow = signal<TableReportRow | null>(null);
  protected readonly stateRow = signal<TableReportRow | null>(null);
  protected readonly deleteRow = signal<TableReportRow | null>(null);

  /** the dialogs hold a row, so after a change the row is looked up again to show the new state */
  protected readonly liveRatingRow = computed(() => this.findRow(this.ratingRow()));
  protected readonly liveStateRow = computed(() => this.findRow(this.stateRow()));

  /** nothing to show yet, and the first page is on its way */
  protected readonly isFirstLoad = computed(() => this.rows().length === 0 && this.isLoading());

  /** the page numbers of the footer, `null` is a "…" */
  protected readonly pageItems = computed(() => this.toPageItems(this.page(), this.totalPages()));

  protected goTo(page: number): void {
    if (this.isLoading() || page < 1 || page > this.totalPages() || page === this.page()) return;

    this.pageChange.emit(page);
  }

  // private
  private findRow(row: TableReportRow | null): TableReportRow | null {
    return row ? (this.rows().find((r) => r.report.id === row.report.id) ?? row) : null;
  }

  private toPageItems(current: number, total: number): (number | null)[] {
    const pages = [...new Set([1, total, current - 1, current, current + 1])]
      .filter((p) => p >= 1 && p <= total)
      .sort((a, b) => a - b);

    const items: (number | null)[] = [];
    let previous = 0;

    for (const page of pages) {
      // a gap of one page shows that page, a bigger gap shows "..."
      if (page - previous === 2) items.push(previous + 1);
      else if (page - previous > 2) items.push(null);

      items.push(page);
      previous = page;
    }

    return items;
  }
}
