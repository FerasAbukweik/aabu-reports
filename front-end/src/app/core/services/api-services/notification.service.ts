import { inject, Injectable, signal } from '@angular/core';
import { defer, EMPTY, finalize, Observable, Subscription, tap } from 'rxjs';
import { NotificationDTO } from '../../DTOs/notification/notification-dto';
import { toastError } from '../../utils/error-toast';
import { getNotificationMeta } from '../../utils/notification-utils';
import { NotificationApiService } from '../api/notification-api.service';
import { NotificationHubService } from '../hubs/notification-hub.service';
import { ToastService } from '../local/toast.service';

/** how many notifications are loaded each time */
const NOTIFICATIONS_PAGE_SIZE = 10;

@Injectable({ providedIn: 'root' })
export class NotificationService {
  // DI
  private notificationApi = inject(NotificationApiService);
  private hub = inject(NotificationHubService);
  private toast = inject(ToastService);

  // state

  /** the loaded notifications, newest first */
  private readonly _notifications = signal<readonly NotificationDTO[]>([]);
  public readonly notifications = this._notifications.asReadonly();

  /**
   * how many notifications the user did not see yet (the number on the bell).
   * It comes from the api, not from counting the list, because the list only holds the pages loaded so far.
   */
  private readonly _unreadCount = signal(0);
  public readonly unreadCount = this._unreadCount.asReadonly();

  /** a page is loading (the first one or the next ones, the list stays on the screen) */
  private readonly _isLoading = signal(false);
  public readonly isLoading = this._isLoading.asReadonly();

  /** the api returns no total, so a full page means there may be more (a short or empty page stops it) */
  private readonly _isMoreAvailable = signal(true);
  public readonly isMoreAvailable = this._isMoreAvailable.asReadonly();

  /**
   * the last page failed to load. The list asks for pages itself when its sentinel is on the screen, and the
   * sentinel comes back after every request, so without this it would ask again and again: the user retries by hand.
   */
  private readonly _hasLoadFailed = signal(false);
  public readonly hasLoadFailed = this._hasLoadFailed.asReadonly();

  private isStarted = false;
  private hubSubscriptions = new Subscription();

  // methods

  /**
   * Starts the real time notifications: connects to the hub and loads the unread count (the number on the bell).
   * The list itself is not loaded here, the panel loads its pages with `loadMore` when it is on the screen.
   * Notifications pushed before that are already in the list, and the first page continues after them.
   * Does nothing if it already started. Call it when the user is logged in, and `stop` when they log out.
   */
  public async start(): Promise<void> {
    if (this.isStarted) return;
    this.isStarted = true;

    this.hubSubscriptions.add(this.hub.onNotificationReceived$.subscribe((n) => this.receive(n)));
    this.hubSubscriptions.add(this.hub.onReconnected$.subscribe(() => this.refreshAfterReconnect()));

    await this.hub.startConnection();

    // stopped while connecting
    if (!this.isStarted) return;

    this.refreshUnreadCount();
  }

  /** Disconnects from the hub and clears the state (e.g. when the user leaves the logged in pages). */
  public async stop(): Promise<void> {
    if (!this.isStarted) return;
    this.isStarted = false;

    this.hubSubscriptions.unsubscribe();
    this.hubSubscriptions = new Subscription();

    this.reset();
    await this.hub.stopConnection();
  }

  /**
   * Loads the next page of the notifications of the current user (GET /notification) and adds it to
   * the end of `notifications`. The first call loads the first page.
   * Does nothing when there is no more, or a page is already loading.
   * A call after a failure is a retry (the failed flag is cleared when it starts).
   * On failure it shows an error toast and rethrows the error.
   */
  public loadMore(): Observable<readonly NotificationDTO[]> {
    // defer: the state is set when the request is subscribed, not when this method is called
    return defer(() => {
      if (!this._isMoreAvailable() || this._isLoading()) return EMPTY;

      this._hasLoadFailed.set(false);
      this._isLoading.set(true);

      // the notifications already loaded are the ones taken
      const taken = this._notifications().length;

      return this.notificationApi.getMine({ taken, sectionSize: NOTIFICATIONS_PAGE_SIZE }).pipe(
        tap((page) => {
          this.addMissing(page);
          this._isMoreAvailable.set(page.length === NOTIFICATIONS_PAGE_SIZE);
        }),
        finalize(() => this._isLoading.set(false)),
        tap({ error: () => this._hasLoadFailed.set(true) }),
        toastError(this.toast, 'تعذر تحميل الإشعارات'),
      );
    });
  }

  /**
   * Marks every notification as seen (PUT /notification/read-all).
   * It changes on the screen right away, and goes back if the request fails.
   * Does nothing when there is nothing unread.
   * On failure it shows an error toast and rethrows the error.
   */
  public markAllAsRead(): Observable<void> {
    // defer: the screen changes when the request is subscribed, not when this method is called
    return defer(() => {
      const previousCount = this._unreadCount();
      if (previousCount === 0) return EMPTY;

      const unreadIds = new Set(this._notifications().filter((n) => !n.isRead).map((n) => n.id));

      this._notifications.update((list) => list.map((n) => (n.isRead ? n : { ...n, isRead: true })));
      this._unreadCount.set(0);

      return this.notificationApi.markAllAsRead().pipe(
        tap({
          error: () => {
            // only the ones that were unread go back, a notification that arrived in the meantime stays as it is
            this._notifications.update((list) => list.map((n) => (unreadIds.has(n.id) ? { ...n, isRead: false } : n)));
            this._unreadCount.update((count) => count + previousCount);
          },
        }),
        toastError(this.toast, 'تعذر تحديد الإشعارات كمقروءة'),
      );
    });
  }

  // private

  /** a notification pushed by the hub: it goes on top, the number on the bell goes up, and a toast shows it */
  private receive(notification: NotificationDTO): void {
    if (this._notifications().some((n) => n.id === notification.id)) return;

    this._notifications.update((list) => [notification, ...list]);
    if (!notification.isRead) this._unreadCount.update((count) => count + 1);

    const meta = getNotificationMeta(notification.type);
    const message = notification.subject ? `${notification.message} • ${notification.subject}` : notification.message;
    this.toast.show(meta.status, meta.label, message, { icon: meta.icon });
  }

  /** the connection was lost for a while: load what was missed (the first page and the count), without touching the rest */
  private refreshAfterReconnect(): void {
    this.refreshUnreadCount();

    this.notificationApi.getMine({ taken: 0, sectionSize: NOTIFICATIONS_PAGE_SIZE }).subscribe({
      next: (page) => this.addMissing(page),
      error: () => {}, // no toast, the old list stays on the screen
    });
  }

  private refreshUnreadCount(): void {
    this.notificationApi.getUnreadCount().subscribe({
      next: (count) => this._unreadCount.set(count),
      error: () => {},
    });
  }

  /** adds the notifications that are not in the list yet, and keeps it newest first */
  private addMissing(incoming: readonly NotificationDTO[]): void {
    this._notifications.update((list) => {
      const ids = new Set(list.map((n) => n.id));
      const missing = incoming.filter((n) => !ids.has(n.id));
      if (missing.length === 0) return list;

      // by the time, not by the text: the api trims the zeros of the fraction, so "…12.5Z" and "…12.45Z" do not sort as text
      return [...list, ...missing].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
    });
  }

  private reset(): void {
    this._notifications.set([]);
    this._unreadCount.set(0);
    this._isLoading.set(false);
    this._isMoreAvailable.set(true);
    this._hasLoadFailed.set(false);
  }
}
