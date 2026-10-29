import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { locationStatusLabel, locationStatusTone } from '../../../../core/utils/location-util';
import { googleMapsUrl } from '../../../../core/utils/map.utils';
import { SelectedLocation } from '../building.model';

@Component({
  selector: 'app-building-details',
  templateUrl: './building-details.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BuildingDetailsComponent {
  readonly building = input.required<SelectedLocation>();

  readonly editRequested = output<void>();
  readonly focusRequested = output<void>();
  readonly ticketsRequested = output<void>();
  readonly deleteRequested = output<void>();

  protected readonly stateLabel = locationStatusLabel;
  protected readonly stateTone = locationStatusTone;

  protected readonly mapsUrl = computed(() => googleMapsUrl(this.building().coordinates));
}
