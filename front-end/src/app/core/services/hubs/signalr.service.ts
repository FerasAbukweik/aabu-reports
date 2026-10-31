import { inject, Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { firstValueFrom, Subject } from 'rxjs';
import { URLs } from '../../constants/URLs';
import { AuthService } from '../api-services/auth.service';

export type HubState = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'failed';

/**
 * The ONE SignalR connection of the app (notifications and the chat of the reports share it).
 * It is opened when the user logs in and closed when they log out (see NotificationService), it is NOT closed when
 * the user moves between reports.
 *
 * It only owns the life of the connection (start, stop, reconnect, state). What is listened to and what is sent
 * is up to the services that use `connection` (NotificationHubService, MessageHubService).
 */
@Injectable({ providedIn: 'root' })
export class SignalRService {
  // DI
  private readonly authService = inject(AuthService);

  /** built once and never replaced: the handlers registered with `connection.on` live as long as the app */
  readonly connection = new signalR.HubConnectionBuilder()
    .withUrl(URLs.hub, { withCredentials: true })
    // right away, then after 2s, 5s, 10s, 30s and 60s, then it gives up (onclose)
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000, 60_000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  // state
  private readonly _state = signal<HubState>('idle');
  readonly state = this._state.asReadonly();

  /** the connection was lost (or never came up) and it is back: what happened in between is missed */
  private hasMissedEvents = false;

  private starting: Promise<void> | null = null;

  // Observables

  /**
   * the connection came back after it was lost: it is a NEW connection, so the groups it was in are gone, and what
   * was sent in between is missed. The listeners should join their group again and load what they missed.
   */
  readonly onReconnected$ = new Subject<void>();

  /** the connection was lost and the automatic reconnects gave up */
  readonly onConnectionLost$ = new Subject<void>();

  constructor() {
    this.connection.onreconnecting(() => this._state.set('reconnecting'));

    this.connection.onreconnected(() => {
      this._state.set('connected');
      this.onReconnected$.next();
    });

    // `error` is only set when the connection was lost (and the reconnects gave up), not when stop closed it
    this.connection.onclose((error) => {
      if (!error) return;

      this.hasMissedEvents = true;
      this._state.set('failed');
      this.onConnectionLost$.next();
    });
  }

  // methods

  /**
   * Connects. Does nothing if it is already connected, and waits for it if it is connecting.
   * It does not throw: `state` says how it went. It is also the retry after the state became 'failed'.
   */
  start(): Promise<void> {
    if (this.starting) return this.starting;
    if (this._state() === 'connected' || this._state() === 'reconnecting') return Promise.resolve();

    this._state.set('connecting');
    this.starting = this.connect().finally(() => (this.starting = null));

    return this.starting;
  }

  /** Closes the connection (the user logged out). */
  async stop(): Promise<void> {
    this._state.set('idle');
    this.hasMissedEvents = false;

    try {
      await this.connection.stop();
    } catch {
      // nothing to do, the connection is closed or the page is closing
    }
  }

  // private

  private async connect(): Promise<void> {
    try {
      await this.startWithRefresh();
    } catch {
      // stop was called while it was connecting, that is not a failure
      if (this._state() !== 'connecting') return;

      this.hasMissedEvents = true;
      this._state.set('failed');
      return;
    }

    // stop was called while it was connecting
    if (this._state() !== 'connecting') return;

    this._state.set('connected');

    if (this.hasMissedEvents) {
      this.hasMissedEvents = false;
      this.onReconnected$.next();
    }
  }

  /**
   * The hub does not go through the http interceptors, so an expired access token is not refreshed for it.
   * If the first try fails, the token is refreshed by hand and it tries once more (a real failure fails again).
   */
  private async startWithRefresh(): Promise<void> {
    try {
      await this.connection.start();
    } catch (error) {
      // stop closed it while it was connecting, do not connect again
      if (this._state() !== 'connecting') throw error;

      await firstValueFrom(this.authService.refresh());
      await this.connection.start();
    }
  }
}
