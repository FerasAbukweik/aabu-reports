import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/services/api-services/auth.service';
import { LayoutService } from '../../../core/services/local/layout.service';
import { MaintenanceStatsService } from '../../../core/services/local/maintenance-stats.service';
import { ThemeService } from '../../../core/services/local/theme.service';
import { SidebarComponent } from '../../components/sidebar/sidebar.component';

/** Header + sidebar. The routed page is rendered inside <main>. */
@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent],
  templateUrl: './main-layout.component.html',
  host: { '(document:keydown.escape)': 'layoutService.closeNav()' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MainLayoutComponent {
  private readonly authService = inject(AuthService);
  protected readonly themeService = inject(ThemeService);
  protected readonly layoutService = inject(LayoutService);

  protected readonly stats = inject(MaintenanceStatsService).stats;
  protected readonly notificationCount = this.layoutService.notificationCount;
  protected readonly user = this.authService.currentUser;
}
