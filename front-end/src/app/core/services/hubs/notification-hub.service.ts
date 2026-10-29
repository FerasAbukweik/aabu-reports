import { inject, Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { firstValueFrom, Subject } from 'rxjs';
import { URLs } from '../../constants/URLs';
import { NotificationDTO } from '../../DTOs/notification/notification-dto';
import { AuthService } from '../api-services/auth.service';
import { ToastService } from '../local/toast.service';

/**
 * The SignalR connection of the notifications (server to client only: the client sends nothing).
 * The server pushes every new notification of the logged in user to "ReceiveNotification".
 * The user is known from the access token cookie, so no user id is sent.
 */
@Injectable({ providedIn: 'root' })
export class NotificationHubService {
  // DI
  private readonly toast = inject(ToastService);
  private readonly authService = inject(AuthService);

  // Hub connection
  private connection: signalR.HubConnection | null = null;

  // Observables
  /** a new notification arrived */
  readonly onNotificationReceived$ = new Subject<NotificationDTO>();

  /** the connection came back after it was lost: what was pushed in between is lost, the listener should load it again */
  readonly onReconnected$ = new Subject<void>();

  // methods

  /** Connects to the hub. Does nothing if it is already connected (or connecting). Shows a toast if it fails. */
  async startConnection(): Promise<void> {
    if (this.connection) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(URLs.notificationHub, { withCredentials: true })
      // right away, then after 2s, 5s, 10s, 30s and 60s, then it gives up (onclose)
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000, 60_000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.registerHandlers(connection);
    this.connection = connection;

    try {
      await this.startWithRefresh(connection);
    } catch {
      // stopConnection was called while it was connecting, that is not a failure
      if (this.connection !== connection) return;

      this.connection = null;
      this.toast.error('تعذر الاتصال بخدمة الإشعارات', 'ستظهر الإشعارات الجديدة بعد إعادة تحميل الصفحة');
    }
  }

  async stopConnection(): Promise<void> {
    const connection = this.connection;
    if (!connection) return;

    this.connection = null;

    try {
      await connection.stop();
    } catch {
      // nothing to do, the connection is closed or the page is closing
    }
  }

  // private

  /**
   * The hub does not go through the http interceptors, so an expired access token is not refreshed for it.
   * If the first try fails, the token is refreshed by hand and it tries once more (a real failure fails again).
   */
  private async startWithRefresh(connection: signalR.HubConnection): Promise<void> {
    try {
      await connection.start();
    } catch (error) {
      // stopConnection closed it while it was connecting, do not connect again
      if (this.connection !== connection) throw error;

      await firstValueFrom(this.authService.refresh());
      await connection.start();
    }
  }

  private registerHandlers(connection: signalR.HubConnection): void {
    connection.on('ReceiveNotification', (notification: NotificationDTO) => {
      this.onNotificationReceived$.next(notification);
    });

    connection.onreconnected(() => this.onReconnected$.next());

    // `error` is only set when the connection was lost (and the reconnects gave up), not when stopConnection closed it
    connection.onclose((error) => {
      if (!error) return;

      this.connection = null;
      this.toast.error('انقطع الاتصال بخدمة الإشعارات', 'أعد تحميل الصفحة لاستقبال الإشعارات الجديدة');
    });
  }
}
