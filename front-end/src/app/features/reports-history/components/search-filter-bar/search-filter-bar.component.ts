import {
  ChangeDetectionStrategy,
  Component,
  afterNextRender,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ReportFilterDTO } from '../../../../core/DTOs/report/report-filter-dto';
import { ReportCategory } from '../../../../core/enums/report-category.enum';
import { ReportState } from '../../../../core/enums/report-state.enum';
import { FILTERS } from '../../reports-history.data';
import { FilterValues } from './search-filter-bar.model';

/** what the search box, the dropdowns and the location say -> the filters of the api (an empty value is "all" and is left out) */
function toFilter(
  query: string,
  { state, category }: FilterValues,
  locationId: string,
): ReportFilterDTO {
  return {
    search: query.trim() || undefined,
    state: state ? (Number(state) as ReportState) : undefined,
    category: category ? (Number(category) as ReportCategory) : undefined,
    locationId: locationId || undefined,
  };
}

/**
 * The search box, the dropdowns and the apply / reset buttons of the reports history. It owns the filters: the page only
 * receives them on `applied` (once when it opens, with the location the map page sent in `?l_id=`, then on every apply or reset)
 * and loads its data with them.
 *
 *   <app-search-filter-bar [resultsCount]="reports().length" [isLoading]="isLoading()" (applied)="load($event)" />
 */
@Component({
  selector: 'app-search-filter-bar',
  templateUrl: './search-filter-bar.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class SearchFilterBarComponent {
  // DI
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // input
  /** a page is loading: the apply button waits */
  readonly isLoading = input(false);
  /** how many results the page shows, the chip is hidden when it is not given */
  readonly resultsCount = input<number>();

  // output
  /** the filters to load the data with */
  readonly applied = output<ReportFilterDTO>();

  // state
  protected readonly filters = FILTERS;

  /** what the search box and the dropdowns show right now (not applied until the apply button is pressed) */
  protected readonly query = signal('');
  protected readonly values = signal<FilterValues>({});

  /** the location the map page sent us here with (`?l_id=`): part of the filters until the reset button */
  private readonly locationId = signal<string>(this.route.snapshot.queryParams['l_id'] ?? '');

  /** the filters the page was told to load with last */
  private readonly appliedFilter = signal<ReportFilterDTO>({});

  private readonly picked = computed(() =>
    toFilter(this.query(), this.values(), this.locationId()),
  );

  /** the search box, the dropdowns or the location differ from what was applied */
  private readonly hasUnappliedChanges = computed(() => {
    const picked = this.picked();
    const applied = this.appliedFilter();

    return (
      picked.search !== applied.search ||
      picked.state !== applied.state ||
      picked.category !== applied.category ||
      picked.locationId !== applied.locationId
    );
  });

  protected readonly canApply = computed(() => this.hasUnappliedChanges() && !this.isLoading());

  protected readonly hasActiveFilters = computed(
    () =>
      this.locationId() !== '' ||
      this.query().trim() !== '' ||
      Object.values(this.values()).some((value) => value !== ''),
  );

  constructor() {
    // the first load: after the first render, so the page does not change what it showed while it was being checked
    afterNextRender(() => this.emit());
  }

  // methods
  protected valueOf(key: string): string {
    return this.values()[key] ?? '';
  }

  protected setFilter(key: string, value: string): void {
    this.values.update((current) => ({ ...current, [key]: value }));
  }

  protected apply(): void {
    if (this.canApply()) this.emit();
  }

  /** empties the search box, the dropdowns and the location, then the page follows unless it already shows everything */
  protected reset(): void {
    this.query.set('');
    this.values.set({});
    this.locationId.set('');

    if (this.hasUnappliedChanges()) this.emit();
    this.clearLocationParam();
  }

  // private
  private emit(): void {
    const filter = this.picked();

    this.appliedFilter.set(filter);
    this.applied.emit(filter);
  }

  /** drops `?l_id=` from the address bar without reloading or adding a history entry */
  private clearLocationParam(): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {},
      queryParamsHandling: 'replace',
      replaceUrl: true,
    });
  }
}
