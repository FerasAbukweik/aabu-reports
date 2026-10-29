import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  output,
  untracked,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { BuildingFormValue } from '../building.model';

@Component({
  selector: 'app-building-form',
  imports: [ReactiveFormsModule],
  templateUrl: './building-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BuildingFormComponent {
  private readonly fb = inject(FormBuilder);

  readonly mode = input.required<'add' | 'edit'>();
  /** shown in the header while editing */
  readonly buildingTitle = input('');
  /** the form is reset to this value every time a new object arrives */
  readonly seed = input.required<BuildingFormValue>();
  readonly saving = input(false);

  /** emits the raw value once the form is valid */
  readonly submitted = output<BuildingFormValue>();
  readonly cancelled = output<void>();

  protected readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(150)]],
    lat: [0, [Validators.required, Validators.min(-90), Validators.max(90)]],
    lng: [0, [Validators.required, Validators.min(-180), Validators.max(180)]],
  });

  constructor() {
    effect(() => {
      const seed = this.seed();
      untracked(() => this.form.reset(seed));
    });
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitted.emit(this.form.getRawValue());
  }
}
