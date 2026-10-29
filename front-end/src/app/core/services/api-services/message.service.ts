import { computed, inject, Injectable, OnDestroy, signal } from '@angular/core';
import { defer, EMPTY, finalize, Observable, Subscription, tap } from 'rxjs';
import { MessageDTO, messageKey } from '../../DTOs/message/message-dto';
import { toastError } from '../../utils/error-toast';
import { MessageApiService } from '../api/message-api.service';
import { MessageHubService } from '../hubs/message-hub.service';
import { ToastService } from '../local/toast.service';
import { AuthService } from './auth.service';

/** how many messages are loaded each time */
const MESSAGES_PAGE_SIZE = 20;

/** while the user types, the others are told again after this long (so their "typing..." does not run out) */
const TYPING_RESEND_MS = 3_000;
/** the user stopped typing after this long without a key */
const TYPING_IDLE_MS = 2_000;
/** someone else's "typing..." goes away after this long without news (the "stopped" message can get lost) */
const TYPING_EXPIRE_MS = 6_000;

/**
 * The state of the chat of ONE report: the old messages (lazy loaded, newest first from the api), the live ones from
 * the hub, and who is typing. It is not a root service: the chat component provides its own, together with the hub.
 */
@Injectable()
export class MessageService implements OnDestroy {
  // DI
  private messageApi = inject(MessageApiService);
  private hub = inject(MessageHubService);
  private authService = inject(AuthService);
  private toast = inject(ToastService);

  // state

  /** the loaded messages, OLDEST first (the order of the screen) */
  private readonly _messages = signal<readonly MessageDTO[]>([]);
  public readonly messages = this._messages.asReadonly();

  /** a page is loading */
  private readonly _isLoading = signal(false);
  public readonly isLoading = this._isLoading.asReadonly();

  /** the api returns no total, so a full page means there may be more (a short or empty page stops it) */
  private readonly _isMoreAvailable = signal(true);
  public readonly isMoreAvailable = this._isMoreAvailable.asReadonly();

  /** the last page failed to load: the sentinel stops asking, the user retries by hand */
  private readonly _hasLoadFailed = signal(false);
  public readonly hasLoadFailed = this._hasLoadFailed.asReadonly();

  /** the first page was answered (loaded or failed): before it the chat shows its skeleton */
  private readonly _isFirstPageDone = signal(false);
  public readonly isFirstPageDone = this._isFirstPageDone.asReadonly();

  /** the state of the live connection */
  public readonly connectionState = this.hub.state;

  /** the names of the others who are typing right now */
  private readonly typingUserIds = signal<readonly string[]>([]);
  public readonly typingNames = computed(() => {
    const messages = this._messages();

    return this.typingUserIds().map((id) => messages.find((m) => m.senderId === id)?.senderName ?? 'مستخدم');
  });

  private reportId: string | null = null;
  private subscriptions = new Subscription();

  // typing
  private isTypingSent = false;
  private lastTypingSentAt = 0;
  private idleTimer: ReturnType<typeof setTimeout> | null = null;
  private readonly expireTimers = new Map<string, ReturnType<typeof setTimeout>>();

  // methods

  /** the logged in user, to know which messages are their own */
  public isOwn(message: MessageDTO): boolean {
    const me = this.authService.currentUser()?.userId;

    return !!me && message.senderId.toLowerCase() === me.toLowerCase();
  }

  /**
   * Opens the chat of a report: connects to the hub and loads the first page of the old messages.
   * Opening again (another report) closes the one before and clears the state.
   */
  public open(reportId: string): void {
    this.close();
    this.reportId = reportId;

    this.subscriptions.add(
      this.hub.onMessageReceived$.subscribe((message) => {
        // the hub of this chat only sends its own report, this is just a guard
        if (message.reportId.toLowerCase() === reportId.toLowerCase()) this.addMissing([message]);
      }),
    );
    this.subscriptions.add(this.hub.onReconnected$.subscribe(() => this.refreshAfterReconnect()));
    this.subscriptions.add(this.hub.onTyping$.subscribe((id) => this.someoneTyping(id)));
    this.subscriptions.add(this.hub.onStoppedTyping$.subscribe((id) => this.someoneStopped(id)));

    void this.hub.start(reportId);

    this.loadMore().subscribe({ error: () => {} }); // already shown as a toast
  }

  /** the connection failed or was lost for good: connects again */
  public reconnect(): void {
    if (!this.reportId) return;

    void this.hub.start(this.reportId).then(() => {
      if (this.hub.state() === 'connected') this.refreshAfterReconnect();
    });
  }

  /** Disconnects and clears the state (the chat is closed or another report is opened). */
  public close(): void {
    this.subscriptions.unsubscribe();
    this.subscriptions = new Subscription();

    this.clearTypingTimers();
    this.reportId = null;

    void this.hub.stop();

    this._messages.set([]);
    this._isLoading.set(false);
    this._isMoreAvailable.set(true);
    this._hasLoadFailed.set(false);
    this._isFirstPageDone.set(false);
    this.typingUserIds.set([]);
  }

  /**
   * Loads the next (older) page of the chat (GET /message) and adds it to the top of `messages`.
   * Does nothing when there is no more, or a page is already loading.
   * A call after a failure is a retry (the failed flag is cleared when it starts).
   * On failure it shows an error toast and rethrows the error.
   */
  public loadMore(): Observable<readonly MessageDTO[]> {
    // defer: the state is set when the request is subscribed, not when this method is called
    return defer(() => {
      const reportId = this.reportId;
      if (!reportId || !this._isMoreAvailable() || this._isLoading()) return EMPTY;

      this._hasLoadFailed.set(false);
      this._isLoading.set(true);

      // the messages already loaded (the live ones too) are the newest ones, so they are the ones taken
      const taken = this._messages().length;

      return this.messageApi.getByReport(reportId, { taken, sectionSize: MESSAGES_PAGE_SIZE }).pipe(
        tap((page) => {
          // the chat was closed or changed while the page was on its way
          if (this.reportId !== reportId) return;

          this.addMissing(page);
          this._isMoreAvailable.set(page.length === MESSAGES_PAGE_SIZE);
        }),
        finalize(() => {
          if (this.reportId !== reportId) return;

          this._isLoading.set(false);
          this._isFirstPageDone.set(true);
        }),
        tap({ error: () => this.reportId === reportId && this._hasLoadFailed.set(true) }),
        toastError(this.toast, 'تعذر تحميل المراسلات'),
      );
    });
  }

  /**
   * Sends a message to the chat through the hub. It is NOT added here: the server sends it back to everyone in the
   * chat (this user too), and it is added then. Resolves true when the server took it.
   * On failure it shows an error toast and resolves false.
   */
  public async send(content: string): Promise<boolean> {
    this.stopTyping();

    try {
      await this.hub.sendMessage(content);
      return true;
    } catch {
      this.toast.error('تعذر إرسال الرسالة', 'تحقق من اتصالك وحاول مرة أخرى');
      return false;
    }
  }

  /** the text of the box changed: tells the others that the user is typing (and that they stopped, after a pause) */
  public draftChanged(text: string): void {
    if (!text.trim()) {
      this.stopTyping();
      return;
    }

    const now = Date.now();
    if (!this.isTypingSent || now - this.lastTypingSentAt > TYPING_RESEND_MS) {
      this.isTypingSent = true;
      this.lastTypingSentAt = now;
      this.hub.notifyTyping();
    }

    if (this.idleTimer) clearTimeout(this.idleTimer);
    this.idleTimer = setTimeout(() => this.stopTyping(), TYPING_IDLE_MS);
  }

  ngOnDestroy(): void {
    this.stopTyping();
    this.close();
  }

  // private

  private stopTyping(): void {
    if (this.idleTimer) clearTimeout(this.idleTimer);
    this.idleTimer = null;

    if (!this.isTypingSent) return;

    this.isTypingSent = false;
    this.hub.notifyStoppedTyping();
  }

  private someoneTyping(userId: string): void {
    this.typingUserIds.update((ids) => (ids.includes(userId) ? ids : [...ids, userId]));

    clearTimeout(this.expireTimers.get(userId));
    this.expireTimers.set(
      userId,
      setTimeout(() => this.someoneStopped(userId), TYPING_EXPIRE_MS),
    );
  }

  private someoneStopped(userId: string): void {
    clearTimeout(this.expireTimers.get(userId));
    this.expireTimers.delete(userId);

    this.typingUserIds.update((ids) => ids.filter((id) => id !== userId));
  }

  private clearTypingTimers(): void {
    if (this.idleTimer) clearTimeout(this.idleTimer);
    this.idleTimer = null;
    this.isTypingSent = false;

    this.expireTimers.forEach((timer) => clearTimeout(timer));
    this.expireTimers.clear();
  }

  /** the connection was lost for a while: load what was missed (the newest page), without touching the rest */
  private refreshAfterReconnect(): void {
    const reportId = this.reportId;
    if (!reportId) return;

    this.messageApi.getByReport(reportId, { taken: 0, sectionSize: MESSAGES_PAGE_SIZE }).subscribe({
      next: (page) => {
        if (this.reportId === reportId) this.addMissing(page);
      },
      error: () => {}, // no toast, the old messages stay on the screen
    });
  }

  /** adds the messages that are not in the list yet, and keeps it oldest first */
  private addMissing(incoming: readonly MessageDTO[]): void {
    this._messages.update((list) => {
      const keys = new Set(list.map(messageKey));
      const missing = incoming.filter((m) => !keys.has(messageKey(m)));
      if (missing.length === 0) return list;

      // by the time, not by the text: the api trims the zeros of the fraction, so "…12.5Z" and "…12.45Z" do not sort as text
      return [...list, ...missing].sort((a, b) => new Date(a.sentAt).getTime() - new Date(b.sentAt).getTime());
    });
  }
}
