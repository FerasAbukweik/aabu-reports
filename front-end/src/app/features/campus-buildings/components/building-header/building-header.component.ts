import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-building-header',
  templateUrl: './building-header.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BuildingHeaderComponent {
  readonly count = input.required<number>();
}
