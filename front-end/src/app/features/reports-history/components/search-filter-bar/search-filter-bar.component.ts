import { ChangeDetectionStrategy, Component, computed, input, model, output } from '@angular/core';
import { FilterConfig, FilterValues } from './search-filter-bar.model';

/**
 * A search box + dropdown filters + a reset button. It only holds the values: the page filters its own data
 * with `query` and `values` (two-way bindings).
 *
 *   <app-search-filter-bar [(query)]="query" [(values)]="values" [filters]="filters" resultsLabel="3 من 10" />
 */
@Component({
  selector: 'app-search-filter-bar',
  templateUrl: './search-filter-bar.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class SearchFilterBarComponent {
  // input
  readonly placeholder = input('بحث...');
  readonly searchLabel = input('بحث');
  readonly filters = input<readonly FilterConfig[]>([]);
  /** the text of the results chip, e.g. "3 من 10". The chip is hidden when empty */
  readonly resultsLabel = input<string>();

  /** shows the apply button: the page then filters its data on `applied`, not on every change of `values` */
  readonly showApply = input(false);
  readonly applyDisabled = input(false);

  // output
  /** the apply button was pressed */
  readonly applied = output<void>();
  /** the reset button cleared the search and the filters (the values are already empty when this is emitted) */
  readonly filtersReset = output<void>();

  // two-way
  readonly query = model('');
  readonly values = model<FilterValues>({});

  // protected
  protected readonly hasActiveFilters = computed(
    () => this.query().trim() !== '' || Object.values(this.values()).some((v) => v !== ''),
  );

  protected valueOf(key: string): string {
    return this.values()[key] ?? '';
  }

  protected setFilter(key: string, value: string): void {
    this.values.update((current) => ({ ...current, [key]: value }));
  }

  protected reset(): void {
    this.query.set('');
    this.values.set({});
    this.filtersReset.emit();
  }
}
