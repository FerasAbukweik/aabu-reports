import { Component, effect, inject, untracked } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LocationService } from './core/services/api-services/location.service';
import { MaintenanceStatsService } from './core/services/api-services/maintenance-stats.service';
import { MapReportsService } from './core/services/api-services/map-reports.service';
import { MessageService } from './core/services/api-services/message.service';
import { ReportsTableService } from './core/services/api-services/reports-table.service';
import { AuthService } from './core/services/api-services/auth.service';
import { NotificationService } from './core/services/api-services/notification.service';
import { ToastComponent } from './shared/components/toast/toast.component';

@Component({
  imports: [RouterOutlet, ToastComponent],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  private readonly authService = inject(AuthService);
  private readonly notificationService = inject(NotificationService);

  private readonly locationService = inject(LocationService);
  private readonly maintenanceStatsService = inject(MaintenanceStatsService);
  private readonly mapReportsService = inject(MapReportsService);
  private readonly messageService = inject(MessageService);
  private readonly reportsTableService = inject(ReportsTableService);

  constructor() {
    // Start the shared connection after Me restores the session or verification completes.
    effect(() => {
      const isLoggedIn = this.authService.isLoggedIn();

      untracked(() => {
        if (isLoggedIn) void this.notificationService.start();
        else {
          void this.notificationService.stop();
          this.locationService.reset();
          this.maintenanceStatsService.reset();
          this.mapReportsService.reset();
          this.messageService.reset();
          this.reportsTableService.reset();
        }
      });
    });
  }
}
