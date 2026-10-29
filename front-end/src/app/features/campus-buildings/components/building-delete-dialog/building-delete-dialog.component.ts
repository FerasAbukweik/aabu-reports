import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DialogComponent } from '../../../../shared/components/dialog/dialog.component';

@Component({
  selector: 'app-building-delete-dialog',
  imports: [DialogComponent],
  templateUrl: './building-delete-dialog.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BuildingDeleteDialogComponent {
  readonly open = input.required<boolean>();
  readonly buildingTitle = input<string | undefined>();
  readonly deleting = input(false);

  readonly confirmed = output<void>();
  readonly closed = output<void>();
}
