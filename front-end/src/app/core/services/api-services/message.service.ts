import { inject, Injectable, signal } from '@angular/core';
import { defer, EMPTY, finalize, Observable, tap } from 'rxjs';
import { MessageDTO, messageKey } from '../../DTOs/message/message-dto';
import { toastError } from '../../utils/error-toast';
import { MessageApiService } from '../api/message-api.service';
import { MessageHubService, TypingEvent } from '../hubs/message-hub.service';
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

/** how the loading of the pages of ONE report is going */
interface PageState {
  /** a page is loading */
  isLoading: boolean;
  /** the api returns no total, so a full page means there may be more (a short or empty page stops it) */
  isMoreAvailable: boolean;
}

const INITIAL_PAGE_STATE: PageState = { isLoading: false, isMoreAvailable: true };
const NO_MESSAGES: readonly MessageDTO[] = [];
const NO_IDS: readonly string[] = [];

/** the key of a report in the maps (the case of a guid is not the same everywhere) */
const toKey = (reportId: string) => reportId.toLowerCase();

/**
 * The messages of the reports. It does not know which report is open: every method takes the report id, and the
 * caller (the chat) decides which report it shows.
 *
 * What `loadMore` loads (and the live messages of the joined chat) is cached by report id and never cleared, so
 * coming back to a report shows what was loaded without loading it again.
 * The readers (`messages`, `isLoading`, ...) read signals, so they can be used inside `computed` and templates.
 */
@Injectable({ providedIn: 'root' })
export class MessageService {
  // DI
  private messageApi = inject(MessageApiService);
  private hub = inject(MessageHubService);
  private authService = inject(AuthService);
  private toast = inject(ToastService);

  // state

  /** report id -> its loaded messages, OLDEST first (the order of the screen) */
  private readonly messagesByReport = signal<ReadonlyMap<string, readonly MessageDTO[]>>(new Map());

  /** report id -> how its pages are loading */
  private readonly pageStates = signal<ReadonlyMap<string, PageState>>(new Map());

  /** report id -> the ids of the others who are typing in its chat right now */
  private readonly typingByReport = signal<ReadonlyMap<string, readonly string[]>>(new Map());

  /** the state of the live connection */
  public readonly connectionState = this.hub.state;

  // typing (what this user sends)
  private typingSentFor: string | null = null;
  private lastTypingSentAt = 0;
  private idleTimer: ReturnType<typeof setTimeout> | null = null;

  /** `${reportId}|${userId}` -> the timer that removes someone else's "typing..." */
  private readonly expireTimers = new Map<string, ReturnType<typeof setTimeout>>();

  constructor() {
    // the service lives as long as the app, so it listens for good (the hub only sends the chat that is joined)
    this.hub.onMessageReceived$.subscribe((message) => this.addMissing(message.reportId, [message]));
    this.hub.onTyping$.subscribe((event) => this.someoneTyping(event));
    this.hub.onStoppedTyping$.subscribe((event) => this.someoneStopped(event));
  }

  // readers

  /** the logged in user, to know which messages are their own */
  public isOwn(message: MessageDTO): boolean {
    const me = this.authService.currentUser()?.userId;

    return !!me && message.senderId.toLowerCase() === me.toLowerCase();
  }

  /** the loaded messages of the report, OLDEST first */
  public messages(reportId: string): readonly MessageDTO[] {
    return this.messagesByReport().get(toKey(reportId)) ?? NO_MESSAGES;
  }

  /** a page of the report is loading */
  public isLoading(reportId: string): boolean {
    return this.pageState(reportId).isLoading;
  }

  /** the report may have older messages */
  public isMoreAvailable(reportId: string): boolean {
    return this.pageState(reportId).isMoreAvailable;
  }

  /** the names of the others who are typing right now in the chat of the report */
  public typingNames(reportId: string): string[] {
    const messages = this.messages(reportId);

    return (this.typingByReport().get(toKey(reportId)) ?? NO_IDS).map(
      (id) => messages.find((m) => m.senderId === id)?.senderName ?? 'مستخدم',
    );
  }

  // methods

  /**
   * Loads the next (older) page of the report (GET /message) and adds it to the top of its messages.
   * The first call loads the first page. Does nothing when there is no more, or a page is already loading.
   * A call after a failure is a retry.
   * On failure it shows an error toast and rethrows the error (the caller keeps track of the failure).
   */
  public loadMore(reportId: string): Observable<readonly MessageDTO[]> {
    const id = toKey(reportId);

    // defer: the state is set when the request is subscribed, not when this method is called
    return defer(() => {
      const state = this.pageState(id);
      if (!state.isMoreAvailable || state.isLoading) return EMPTY;

      this.setPageState(id, { isLoading: true });

      // the messages already loaded (the live ones too) are the newest ones, so they are the ones taken
      const taken = this.messages(id).length;

      return this.messageApi.getByReport(id, { taken, sectionSize: MESSAGES_PAGE_SIZE }).pipe(
        tap((page) => {
          this.addMissing(id, page);
          this.setPageState(id, { isMoreAvailable: page.length === MESSAGES_PAGE_SIZE });
        }),
        finalize(() => this.setPageState(id, { isLoading: false })),
        toastError(this.toast, 'تعذر تحميل المراسلات'),
      );
    });
  }

  /** the chat of the report was opened: starts receiving its live messages and typing (the connection stays open) */
  public join(reportId: string): Promise<void> {
    return this.hub.joinReport(toKey(reportId));
  }

  /** the chat of the report was closed: stops receiving it (its messages stay) */
  public leave(reportId: string): Promise<void> {
    const id = toKey(reportId);

    this.stopTyping();
    this.clearTyping(id);

    return this.hub.leaveReport(id);
  }

  /** the connection failed or was lost for good: connects again (the joined chat is joined again when it is up) */
  public reconnect(): void {
    void this.hub.start();
  }

  /**
   * Sends a message to the chat of the report through the hub. It is NOT added here: the server sends it back to
   * everyone in the chat (this user too), and it is added then. Resolves true when the server took it.
   * On failure it shows an error toast and resolves false.
   */
  public async send(reportId: string, content: string): Promise<boolean> {
    this.stopTyping();

    try {
      await this.hub.sendMessage(toKey(reportId), content);
      return true;
    } catch {
      this.toast.error('تعذر إرسال الرسالة', 'تحقق من اتصالك وحاول مرة أخرى');
      return false;
    }
  }

  /** the text of the box changed: tells the others that the user is typing (and that they stopped, after a pause) */
  public draftChanged(reportId: string, text: string): void {
    const id = toKey(reportId);

    if (!text.trim()) {
      this.stopTyping();
      return;
    }

    // the box of another report was being typed in: that one stopped
    if (this.typingSentFor !== id) this.stopTyping();

    const now = Date.now();
    if (this.typingSentFor === null || now - this.lastTypingSentAt > TYPING_RESEND_MS) {
      this.typingSentFor = id;
      this.lastTypingSentAt = now;
      this.hub.notifyTyping(id);
    }

    if (this.idleTimer) clearTimeout(this.idleTimer);
    this.idleTimer = setTimeout(() => this.stopTyping(), TYPING_IDLE_MS);
  }

  // private

  private pageState(reportId: string): PageState {
    return this.pageStates().get(toKey(reportId)) ?? INITIAL_PAGE_STATE;
  }

  private setPageState(reportId: string, changes: Partial<PageState>): void {
    const id = toKey(reportId);

    this.pageStates.update((map) => new Map(map).set(id, { ...(map.get(id) ?? INITIAL_PAGE_STATE), ...changes }));
  }

  /** adds the messages of a report that are not in its list yet, and keeps it oldest first */
  private addMissing(reportId: string, incoming: readonly MessageDTO[]): void {
    const id = toKey(reportId);

    this.messagesByReport.update((map) => {
      const list = map.get(id) ?? NO_MESSAGES;
      const keys = new Set(list.map(messageKey));
      const missing = incoming.filter((m) => !keys.has(messageKey(m)));
      if (missing.length === 0) return map;

      // by the time, not by the text: the api trims the zeros of the fraction, so "…12.5Z" and "…12.45Z" do not sort as text
      const merged = [...list, ...missing].sort((a, b) => new Date(a.sentAt).getTime() - new Date(b.sentAt).getTime());

      return new Map(map).set(id, merged);
    });
  }

  // typing

  /** this user stopped typing: tells the others (if they were told that this user is typing) */
  private stopTyping(): void {
    if (this.idleTimer) clearTimeout(this.idleTimer);
    this.idleTimer = null;

    if (this.typingSentFor === null) return;

    this.hub.notifyStoppedTyping(this.typingSentFor);
    this.typingSentFor = null;
  }

  private someoneTyping(event: TypingEvent): void {
    const id = toKey(event.reportId);
    const timerKey = `${id}|${event.userId}`;

    this.typingByReport.update((map) => {
      const ids = map.get(id) ?? NO_IDS;

      return ids.includes(event.userId) ? map : new Map(map).set(id, [...ids, event.userId]);
    });

    clearTimeout(this.expireTimers.get(timerKey));
    this.expireTimers.set(
      timerKey,
      setTimeout(() => this.someoneStopped(event), TYPING_EXPIRE_MS),
    );
  }

  private someoneStopped(event: TypingEvent): void {
    const id = toKey(event.reportId);
    const timerKey = `${id}|${event.userId}`;

    clearTimeout(this.expireTimers.get(timerKey));
    this.expireTimers.delete(timerKey);

    this.typingByReport.update((map) => {
      const ids = map.get(id) ?? NO_IDS;
      if (!ids.includes(event.userId)) return map;

      return new Map(map).set(id, ids.filter((userId) => userId !== event.userId));
    });
  }

  /** nobody is typing in the chat of the report (it was closed, so it will not say that they stopped) */
  private clearTyping(reportId: string): void {
    const prefix = `${reportId}|`;

    for (const [key, timer] of this.expireTimers) {
      if (!key.startsWith(prefix)) continue;

      clearTimeout(timer);
      this.expireTimers.delete(key);
    }

    this.typingByReport.update((map) => {
      if (!map.has(reportId)) return map;

      const next = new Map(map);
      next.delete(reportId);

      return next;
    });
  }
}
