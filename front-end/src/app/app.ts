import { Component, effect, inject, untracked } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AuthService } from './core/services/api-services/auth.service';
import { NotificationService } from './core/services/api-services/notification.service';
import { ToastComponent } from './shared/components/toast/toast.component';
import { HttpClient } from '@angular/common/http';

@Component({
  imports: [RouterOutlet, ToastComponent],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  private readonly authService = inject(AuthService);
  private readonly notificationService = inject(NotificationService);

  constructor() {
    // the real time notifications live as long as the user is logged in:
    // the app opens logged in (the user is in local storage), or the user logs in later
    effect(() => {
      const isLoggedIn = this.authService.isLoggedIn();

      untracked(() => {
        if (isLoggedIn) void this.notificationService.start();
        else void this.notificationService.stop();
      });
    });
  }
}
