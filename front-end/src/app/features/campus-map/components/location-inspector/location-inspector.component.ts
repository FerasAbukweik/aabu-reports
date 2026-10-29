import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { googleMapsUrl } from '../../../../core/utils/map.utils';
import { LocationDTO } from '../../../../core/DTOs/location/location-dto';
import { ReportMapDTO } from '../../../../core/DTOs/report/report-map-dto';
import { LocationTypeEnum } from '../../../../core/enums/location-type.enum';
import { locationStatusLabel, locationStatusTone } from '../../../../core/utils/location-util';
import { IsVisibleDirective } from '../../../../shared/directives/is-visible.directive';
import { reportCategoryLabel } from '../../../../core/utils/report-category-utils';
import { RouterLink } from '@angular/router';

/** The side panel with the details of the selected location and the report actions. */
@Component({
  selector: 'app-location-inspector',
  imports: [DatePipe, IsVisibleDirective, RouterLink],
  templateUrl: './location-inspector.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex' },
})
export class LocationInspectorComponent {
  /** a building or a custom pin; null when nothing is selected */
  readonly location = input<LocationDTO | null>(null);
  /** the reports of the selected location */
  readonly reports = input<readonly ReportMapDTO[]>([]);
  /** a page of the reports is loading (the first one or the next ones) */
  readonly isLoading = input(false);
  /** false once the api returned a short or empty page: no more to load */
  readonly isMoreAvailable = input(false);
  /** the last page failed to load: nothing is requested by itself until the user retries */
  readonly hasFailed = input(false);
  /** rounded average response time in hours, null while it is unknown (the row is hidden) */
  readonly avgResponseHours = input<number | null>(null);

  readonly reportRequested = output<void>();
  readonly loadMoreReportsRequested = output<void>();
  readonly selectionReset = output<void>();
  readonly focusRequested = output<void>();

  protected readonly reportCategoryLabel = reportCategoryLabel;
  protected readonly stateLabel = locationStatusLabel;
  protected readonly stateTone = locationStatusTone;

  /** a pin (listed or dropped by the user) instead of a building */
  protected readonly isCustomPin = computed(() => this.location()?.type === LocationTypeEnum.Pin);

  protected readonly mapsUrl = computed(() => {
    const loc = this.location();
    return loc ? googleMapsUrl(loc.coordinates) : null;
  });
}
