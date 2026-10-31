import { inject, Injectable } from '@angular/core';
import { Subject } from 'rxjs';
import { NotificationDTO } from '../../DTOs/notification/notification-dto';
import { ToastService } from '../local/toast.service';
import { SignalRService } from './signalr.service';

/**
 * The notifications on the shared SignalR connection (server to client only: the client sends nothing).
 * The server pushes every new notification of the logged in user to "ReceiveNotification".
 * The user is known from the access token cookie, so no user id is sent.
 */
@Injectable({ providedIn: 'root' })
export class NotificationHubService {
  // DI
  private readonly toast = inject(ToastService);
  private readonly signalR = inject(SignalRService);

  // Hub connection (shared)
  private readonly connection = this.signalR.connection;

  // Observables
  /** a new notification arrived */
  readonly onNotificationReceived$ = new Subject<NotificationDTO>();

  /** the connection came back after it was lost: what was pushed in between is lost, the listener should load it again */
  readonly onReconnected$ = this.signalR.onReconnected$.asObservable();

  constructor() {
    this.connection.on('ReceiveNotification', (notification: NotificationDTO) => {
      this.onNotificationReceived$.next(notification);
    });

    this.signalR.onConnectionLost$.subscribe(() =>
      this.toast.error('انقطع الاتصال بخدمة الإشعارات', 'أعد تحميل الصفحة لاستقبال الإشعارات الجديدة'),
    );
  }

  // methods

  /** Connects the shared connection. Does nothing if it is already connected (or connecting). Shows a toast if it fails. */
  async startConnection(): Promise<void> {
    await this.signalR.start();

    if (this.signalR.state() === 'failed') {
      this.toast.error('تعذر الاتصال بخدمة الإشعارات', 'ستظهر الإشعارات الجديدة بعد إعادة تحميل الصفحة');
    }
  }

  /** Closes the shared connection (the user logged out): the chat uses it too, so only call it when the session ends. */
  async stopConnection(): Promise<void> {
    await this.signalR.stop();
  }
}
