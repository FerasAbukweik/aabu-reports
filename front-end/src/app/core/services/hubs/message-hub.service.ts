import { inject, Injectable, OnDestroy, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { firstValueFrom, Subject } from 'rxjs';
import { URLs } from '../../constants/URLs';
import { MessageDTO } from '../../DTOs/message/message-dto';
import { AuthService } from '../api-services/auth.service';

export type MessageHubState = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'failed';

/**
 * The SignalR connection of the chat of ONE report (the server joins the connection to the group of the report
 * named by the `reportId` query param). It is not a root service: the chat provides its own, so the connection
 * lives and dies with the chat.
 *
 * server -> client: ReceiveMessage(message), NotifyTyping(userId), NotifyStoppedTyping(userId)
 * client -> server: SendMessage(content), NotifyTyping(), NotifyStoppedTyping()
 */
@Injectable()
export class MessageHubService implements OnDestroy {
  // DI
  private readonly authService = inject(AuthService);

  // Hub connection
  private connection: signalR.HubConnection | null = null;

  // state
  private readonly _state = signal<MessageHubState>('idle');
  readonly state = this._state.asReadonly();

  // Observables
  /** a message was sent to the chat (also the ones this user sent: the server sends them to everyone in the chat) */
  readonly onMessageReceived$ = new Subject<MessageDTO>();

  /** someone else started typing (their user id) */
  readonly onTyping$ = new Subject<string>();

  /** someone else stopped typing (their user id) */
  readonly onStoppedTyping$ = new Subject<string>();

  /** the connection came back after it was lost: what was sent in between is lost, the listener should load it again */
  readonly onReconnected$ = new Subject<void>();

  // methods

  /** Connects to the chat of the report (the one before is closed). `state` says how it went, it does not throw. */
  async start(reportId: string): Promise<void> {
    await this.stop();

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${URLs.messageHub}?reportId=${encodeURIComponent(reportId)}`, { withCredentials: true })
      // right away, then after 2s, 5s, 10s, 30s and 60s, then it gives up (onclose)
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000, 60_000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.registerHandlers(connection);
    this.connection = connection;
    this._state.set('connecting');

    try {
      await this.startWithRefresh(connection);
      if (this.connection === connection) this._state.set('connected');
    } catch {
      // stop was called while it was connecting, that is not a failure
      if (this.connection !== connection) return;

      this.connection = null;
      this._state.set('failed');
    }
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    this._state.set('idle');

    if (!connection) return;

    try {
      await connection.stop();
    } catch {
      // nothing to do, the connection is closed or the page is closing
    }
  }

  /** Sends a message to the chat. Rejects when the connection is not up, or when the server refuses it. */
  async sendMessage(content: string): Promise<void> {
    if (!this.connection || this._state() !== 'connected') throw new Error('not connected');

    await this.connection.invoke('SendMessage', content);
  }

  /** tells the others this user is typing (fire and forget, a failure is not worth a message) */
  notifyTyping(): void {
    this.fireAndForget('NotifyTyping');
  }

  notifyStoppedTyping(): void {
    this.fireAndForget('NotifyStoppedTyping');
  }

  ngOnDestroy(): void {
    void this.stop();
  }

  // private

  private fireAndForget(method: string): void {
    if (!this.connection || this._state() !== 'connected') return;

    this.connection.send(method).catch(() => {});
  }

  /**
   * The hub does not go through the http interceptors, so an expired access token is not refreshed for it.
   * If the first try fails, the token is refreshed by hand and it tries once more (a real failure fails again).
   */
  private async startWithRefresh(connection: signalR.HubConnection): Promise<void> {
    try {
      await connection.start();
    } catch (error) {
      // stop closed it while it was connecting, do not connect again
      if (this.connection !== connection) throw error;

      await firstValueFrom(this.authService.refresh());
      await connection.start();
    }
  }

  private registerHandlers(connection: signalR.HubConnection): void {
    connection.on('ReceiveMessage', (message: MessageDTO) => this.onMessageReceived$.next(message));
    connection.on('NotifyTyping', (userId: string) => this.onTyping$.next(userId));
    connection.on('NotifyStoppedTyping', (userId: string) => this.onStoppedTyping$.next(userId));

    connection.onreconnecting(() => {
      if (this.connection === connection) this._state.set('reconnecting');
    });

    connection.onreconnected(() => {
      if (this.connection !== connection) return;

      this._state.set('connected');
      this.onReconnected$.next();
    });

    // `error` is only set when the connection was lost (and the reconnects gave up), not when stop closed it
    connection.onclose((error) => {
      if (!error || this.connection !== connection) return;

      this.connection = null;
      this._state.set('failed');
    });
  }
}
