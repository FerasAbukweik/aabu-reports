import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ThemeService } from '../../core/services/local/theme.service';

@Component({
  selector: 'app-theme-toggle',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      (click)="themeService.toggle()"
      class="flex h-10 w-10 items-center justify-center rounded-full border border-border-subtle
             bg-surface-card text-primary shadow-sm transition-colors hover:bg-primary-container"
      [attr.aria-label]="themeService.theme() === 'dark' ? 'التبديل إلى الوضع الفاتح' : 'التبديل إلى الوضع الداكن'"
    >
      <span class="material-symbols-outlined text-xl" aria-hidden="true">
        {{ themeService.theme() === 'dark' ? 'light_mode' : 'dark_mode' }}
      </span>
    </button>
  `,
})
export class ThemeToggleComponent {
  protected readonly themeService = inject(ThemeService);
}