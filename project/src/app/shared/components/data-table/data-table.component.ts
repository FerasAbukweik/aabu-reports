import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  contentChildren,
  input,
  output,
} from '@angular/core';
import { PaginationComponent } from '../pagination/pagination.component';
import { TableColumn } from './data-table.model';
import { TableCellDirective } from './table-cell.directive';

/**
 * Generic table. The page gives the columns and the rows, and a cell template for every column that
 * needs more than plain text. It also handles the paging UI (page numbers, loading, retry, empty).
 *
 *   <app-data-table [columns]="columns" [rows]="rows()" [rowKey]="rowKey" ...>
 *     <ng-template appTableCell="state" let-row>...</ng-template>
 *   </app-data-table>
 */
@Component({
  selector: 'app-data-table',
  imports: [NgTemplateOutlet, PaginationComponent],
  templateUrl: './data-table.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class DataTableComponent<T> {
  // input
  readonly columns = input.required<readonly TableColumn[]>();
  readonly rows = input.required<readonly T[]>();
  /** a unique and stable key of a row, for `track` */
  readonly rowKey = input.required<(row: T) => string | number>();
  readonly ariaLabel = input.required<string>();
  /** extra css classes of a row (e.g. a highlight) */
  readonly rowClass = input<(row: T) => string>();

  /** the page on the screen (the first is 1) and how many pages there are. One page hides the numbers */
  readonly page = input(1);
  readonly totalPages = input(1);

  /** a page is loading: the rows stay, dimmed, and the numbers are disabled */
  readonly isLoading = input(false);
  /** the last page failed to load */
  readonly hasFailed = input(false);

  /** text on the start side of the footer, e.g. "12 reports" */
  readonly footerLabel = input<string>();

  readonly emptyIcon = input('inbox');
  readonly emptyTitle = input('لا توجد بيانات');
  readonly emptyMessage = input<string>();

  // output
  /** the user picked a page number */
  readonly pageChange = output<number>();
  /** the retry button was pressed after a failed load */
  readonly retryRequested = output<void>();

  // protected
  private readonly cells = contentChildren(TableCellDirective);

  protected readonly cellTemplates = computed(
    () => new Map(this.cells().map((cell) => [cell.key(), cell.template])),
  );

  /** nothing to show yet, and the first page is on its way */
  protected readonly isFirstLoad = computed(() => this.rows().length === 0 && this.isLoading());

  protected alignClass(column: TableColumn): string {
    return column.align === 'center' ? 'text-center' : 'text-start';
  }

  protected cellValue(row: T, key: string): unknown {
    return (row as unknown as Record<string, unknown>)[key];
  }
}
