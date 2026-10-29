import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthService } from '../../../core/services/api-services/auth.service';
import { LayoutService } from '../../../core/services/local/layout.service';
import { ThemeService } from '../../../core/services/local/theme.service';
import { NotificationBellComponent } from './notification-bell/notification-bell.component';

/** Fixed top bar: nav toggle (below lg), identity, notifications, theme switch and profile. */
@Component({
  selector: 'app-header',
  imports: [NotificationBellComponent],
  templateUrl: './header.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeaderComponent {
  protected readonly themeService = inject(ThemeService);
  protected readonly layoutService = inject(LayoutService);
  protected readonly user = inject(AuthService).currentUser;
}
