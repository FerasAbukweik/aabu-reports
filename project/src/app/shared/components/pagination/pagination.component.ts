import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

/** Page numbers with "..." for the gaps: 1 ... 4 [5] 6 ... 20. `null` is a gap. */
function pageItems(current: number, total: number): (number | null)[] {
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

/** Page buttons: first, previous, the numbers, next, last. Hidden when there is only one page. */
@Component({
  selector: 'app-pagination',
  templateUrl: './pagination.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaginationComponent {
  /** the current page, the first page is 1 */
  readonly page = input.required<number>();
  readonly totalPages = input.required<number>();
  /** nothing can be clicked (e.g. a page is loading) */
  readonly disabled = input(false);

  readonly pageChange = output<number>();

  protected readonly items = computed(() => pageItems(this.page(), this.totalPages()));

  protected go(page: number): void {
    if (this.disabled() || page < 1 || page > this.totalPages() || page === this.page()) return;

    this.pageChange.emit(page);
  }
}
