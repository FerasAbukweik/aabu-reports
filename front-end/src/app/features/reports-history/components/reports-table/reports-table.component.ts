import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TableReportRow } from '../../../../core/interfaces/table-row';
import { ReportSeverity } from '../../../../core/enums/report-severity.enum';
import { ReportState } from '../../../../core/enums/report-state.enum';
import { locationStatusLabel, locationStatusTone } from '../../../../core/utils/location-util';
import { reportCategoryIcon, reportCategoryLabel } from '../../../../core/utils/report-category-utils';
import { reportSeverityLabel, reportSeverityTone } from '../../../../core/utils/report-severity-utils';
import { googleMapsUrl } from '../../../../core/utils/map.utils';
import { StarRatingComponent } from '../../../../shared/components/star-rating/star-rating.component';

/**
 * The reports table of the history page: fixed columns, the rows, the page numbers, loading, retry and empty state.
 *
 *   <app-reports-table [rows]="rows()" [page]="page()" [totalPages]="totalPages()" ... />
 */
@Component({
  selector: 'app-reports-table',
  imports: [DatePipe, RouterLink, StarRatingComponent],
  templateUrl: './reports-table.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class ReportsTableComponent {
  // input
  readonly rows = input.required<readonly TableReportRow[]>();

  /** the page on the screen (the first is 1) and how many pages there are. One page hides the numbers */
  readonly page = input(1);
  readonly totalPages = input(1);

  /** a page is loading: the rows stay, dimmed, and the numbers are disabled */
  readonly isLoading = input(false);
  /** the last page failed to load */
  readonly hasFailed = input(false);

  /** text on the start side of the footer, e.g. "عرض البلاغات 1 - 10" */
  readonly footerLabel = input<string>();
  readonly emptyMessage = input<string>();

  /** the admin page: the actions column also shows the "change state" and "delete" buttons, and the rate button is hidden (an admin does not rate) */
  readonly isAdmin = input(false);

  // output
  /** the user picked a page number */
  readonly pageChange = output<number>();
  /** the retry button was pressed after a failed load */
  readonly retryRequested = output<void>();
  /** the "rate now" button of a fixed report was pressed */
  readonly rateRequested = output<TableReportRow>();
  /** the admin pressed "change state" of a row */
  readonly stateChangeRequested = output<TableReportRow>();
  /** the admin pressed "delete" of a row */
  readonly deleteRequested = output<TableReportRow>();

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

  /** nothing to show yet, and the first page is on its way */
  protected readonly isFirstLoad = computed(() => this.rows().length === 0 && this.isLoading());

  /** the page numbers of the footer, `null` is a "…" */
  protected readonly pageItems = computed(() => this.toPageItems(this.page(), this.totalPages()));

  protected goTo(page: number): void {
    if (this.isLoading() || page < 1 || page > this.totalPages() || page === this.page()) return;

    this.pageChange.emit(page);
  }

  // private
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
