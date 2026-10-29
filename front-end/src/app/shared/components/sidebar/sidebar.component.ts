import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  ActivatedRouteSnapshot,
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
} from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { LayoutService } from '../../../core/services/local/layout.service';
import {
  ADMIN_NAV_ITEMS,
  ADMIN_SIDEBAR_TITLE,
  NAV_ITEMS,
  SIDEBAR_ARIA_LABEL,
  SIDEBAR_TITLE,
} from './sidebar.data';
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
  private readonly _router = inject(Router);

  /** true while the current route, or any route above it, has `data: { isAdmin: true }` */
  private readonly _isAdmin = toSignal(
    this._router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      startWith(null),
      map(() => this.hasAdminData(this._router.routerState.snapshot.root)),
    ),
    { initialValue: false },
  );

  // protected
  protected readonly ariaLabel = SIDEBAR_ARIA_LABEL;

  protected readonly title = computed(() =>
    this._isAdmin() ? ADMIN_SIDEBAR_TITLE : SIDEBAR_TITLE,
  );
  protected readonly navItems = computed(() => (this._isAdmin() ? ADMIN_NAV_ITEMS : NAV_ITEMS));

  /** walks down the active route tree: the flag may sit on a parent (e.g. the `admin` route) */
  private hasAdminData(route: ActivatedRouteSnapshot | null): boolean {
    for (let r = route; r; r = r.firstChild) {
      if (r.data['isAdmin'] === true) return true;
    }
    return false;
  }

  protected logout(): void {
    // AuthService already shows the error toast if it fails
    this._authService.logout().subscribe({ error: () => {} });
  }
}
