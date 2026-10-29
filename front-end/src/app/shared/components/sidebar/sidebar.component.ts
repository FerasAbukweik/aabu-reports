import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { LayoutService } from '../../../core/services/local/layout.service';
import { NAV_ITEMS, SIDEBAR_ARIA_LABEL, SIDEBAR_TITLE } from './sidebar.data';
import { AuthService } from '../../../core/services/api-services/auth.service';

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SidebarComponent {
  // DI
  protected readonly layout = inject(LayoutService);
  private readonly _authService = inject(AuthService);

  // protected
  protected readonly title = SIDEBAR_TITLE;
  protected readonly ariaLabel = SIDEBAR_ARIA_LABEL;

  protected readonly navItems = NAV_ITEMS;

  protected logout(): void {
    // AuthService already shows the error toast if it fails
    this._authService.logout().subscribe({ error: () => {} });
  }
}
