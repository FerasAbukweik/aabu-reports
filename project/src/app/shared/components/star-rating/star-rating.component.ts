import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Read-only stars (filled up to `value`, out of `max`). */
@Component({
  selector: 'app-star-rating',
  template: `
    <span
      class="inline-flex items-center gap-0.5 text-accent"
      role="img"
      [attr.aria-label]="'التقييم ' + value() + ' من ' + max()"
    >
      @for (filled of stars(); track $index) {
        <span
          class="material-symbols-outlined"
          [class]="filled ? 'text-accent' : 'text-border-muted'"
          [style.font-size.px]="size()"
          style="font-variation-settings: 'FILL' 1"
          aria-hidden="true"
          >star</span
        >
      }
    </span>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StarRatingComponent {
  readonly value = input.required<number>();
  readonly max = input(5);
  /** icon size in px */
  readonly size = input(18);

  protected readonly stars = computed(() =>
    Array.from({ length: this.max() }, (_, i) => i < Math.round(this.value())),
  );
}
