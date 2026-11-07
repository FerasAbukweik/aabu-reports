import { Injectable, effect, signal } from '@angular/core';
import { Theme } from '../../interfaces/theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly _theme = signal<Theme>('light');
  public readonly theme = this._theme.asReadonly();

  constructor() {
    effect(() => {
      const theme = this.theme();
      const root = document.documentElement;

      root.classList.toggle('dark', theme === 'dark');
      root.style.colorScheme = theme;
    });
  }

  toggle(): void {
    this._theme.update((theme) => (theme === 'dark' ? 'light' : 'dark'));
  }
}
