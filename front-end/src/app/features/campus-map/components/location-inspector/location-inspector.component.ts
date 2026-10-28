import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { googleMapsUrl } from '../../../../core/utils/map.utils';
import { LocationDTO } from '../../../../core/DTOs/location/location-dto';
import { ReportDTO } from '../../../../core/DTOs/report/report-dto';
import { locationStatusLabel, locationStatusTone } from '../../../../core/utils/location-util';
import { IsVisableDirective } from '../../../../shared/directives/is-visable.directive';
import { reportCategoryLabel } from '../report-modal/report-modal.data';

/** The side panel with the details of the selected location and the report actions. */
@Component({
  selector: 'app-location-inspector',
  standalone: true,
  imports: [DatePipe, IsVisableDirective],
  templateUrl: './location-inspector.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex' },
})
export class LocationInspectorComponent {
  /** a building or a custom pin; null when nothing is selected */
  readonly location = input<LocationDTO | null>(null);
  readonly isCustomPin = input(false);
  /** the reports of the selected location */
  readonly reports = input<readonly ReportDTO[]>([]);
  /** a page of the reports is loading (the first one or the next ones) */
  readonly isLoading = input(false);
  /** false once the api returned a short or empty page: no more to load */
  readonly isMoreAvailable = input(false);
  readonly avgResponseHours = input.required<number>();

  readonly reportRequested = output<void>();
  readonly loadMoreReportsRequested = output<void>();
  readonly selectionReset = output<void>();
  readonly focusRequested = output<void>();

  protected readonly reportCategoryLabel = reportCategoryLabel;
  protected readonly stateLabel = locationStatusLabel;
  protected readonly stateTone = locationStatusTone;

  protected readonly mapsUrl = computed(() => {
    const loc = this.location();
    return loc ? googleMapsUrl(loc.coordinates) : null;
  });
}
