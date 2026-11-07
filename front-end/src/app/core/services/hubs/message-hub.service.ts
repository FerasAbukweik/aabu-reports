import { inject, Injectable } from '@angular/core';
import { Subject } from 'rxjs';
import { MessageDTO } from '../../DTOs/message/message-dto';
import { SignalRService } from './signalr.service';

/** someone started / stopped typing in the chat of a report */
export interface TypingEvent {
  reportId: string;
  userId: string;
}

/**
 * The chat of the reports on the shared SignalR connection. The chat of a report is a group (named after the report)
 * of the connection: it is joined when the chat opens and left when it closes, the connection itself stays open.
 * Only one chat is open at a time, so only one group is joined at a time.
 *
 * server -> client: ReceiveMessage(message), NotifyTyping(reportId, userId), NotifyStoppedTyping(reportId, userId)
 * client -> server: AddToGroup(reportId), RemoveFromGroup(reportId), SendMessage(reportId, content),
 *                   NotifyTyping(reportId), NotifyStoppedTyping(reportId)
 */
@Injectable({ providedIn: 'root' })
export class MessageHubService {
  // DI
  private readonly signalR = inject(SignalRService);

  // Hub connection (shared)
  private readonly connection = this.signalR.connection;

  // state
  readonly state = this.signalR.state;

  /** the report whose group this connection should be in (null when no chat is open) */
  private currentGroup: string | null = null;

  // Observables
  /** a message was sent to the chat this user joined (also the ones this user sent: the server sends them to everyone in the chat) */
  readonly onMessageReceived$ = new Subject<MessageDTO>();

  /** someone else started typing */
  readonly onTyping$ = new Subject<TypingEvent>();

  /** someone else stopped typing */
  readonly onStoppedTyping$ = new Subject<TypingEvent>();

  /** the connection came back after it was lost (the chat is joined again): what was sent in between is lost, the listener should load it again */
  readonly onReconnected$ = new Subject<void>();

  constructor() {
    this.connection.on('ReceiveMessage', (message: MessageDTO) =>
      this.onMessageReceived$.next(message),
    );
    this.connection.on('NotifyTyping', (reportId: string, userId: string) =>
      this.onTyping$.next({ reportId, userId }),
    );
    this.connection.on('NotifyStoppedTyping', (reportId: string, userId: string) =>
      this.onStoppedTyping$.next({ reportId, userId }),
    );

    // a lost connection comes back as a NEW connection, which is in no group: the chat is joined again, and only then
    // the listener is told (so what it loads is not missed between the load and the join)
    this.signalR.onReconnected$.subscribe(async () => {
      if (this.currentGroup) await this.addToGroup(this.currentGroup);

      this.onReconnected$.next();
    });
  }

  // methods

  /** connects the shared connection again after it failed (it does nothing when it is already up) */
  start(): Promise<void> {
    return this.signalR.start();
  }

  /**
   * Starts receiving the messages and the typing of the report (the one joined before is not left: call `leaveReport` first).
   * It waits for the connection if it is still connecting, and it is joined again after a reconnect. Never throws.
   */
  async joinReport(reportId: string): Promise<void> {
    this.currentGroup = reportId;

    await this.signalR.start();

    // the chat was closed or changed while it was connecting
    if (this.currentGroup !== reportId) return;

    await this.addToGroup(reportId);

    // A slow join can finish after the chat was closed or changed.
    if (this.currentGroup !== reportId) await this.leaveReport(reportId);
  }

  /** Stops receiving the report (the chat was closed). Never throws. */
  async leaveReport(reportId: string): Promise<void> {
    if (this.currentGroup === reportId) this.currentGroup = null;

    try {
      await this.connection.invoke('RemoveFromGroup', reportId);
    } catch {
      // nothing to do: the connection is down, and its groups are gone with it
    }
  }

  /** Sends a message to the chat of the report. Rejects when the connection is not up, or when the server refuses it. */
  sendMessage(reportId: string, content: string): Promise<void> {
    return this.connection.invoke('SendMessage', reportId, content);
  }

  /** tells the others in the chat that this user is typing (fire and forget, a failure is not worth a message) */
  notifyTyping(reportId: string): void {
    this.connection.send('NotifyTyping', reportId).catch(() => {});
  }

  notifyStoppedTyping(reportId: string): void {
    this.connection.send('NotifyStoppedTyping', reportId).catch(() => {});
  }

  // private

  private async addToGroup(reportId: string): Promise<void> {
    try {
      await this.connection.invoke('AddToGroup', reportId);
    } catch {
      // the connection is down (the reconnect joins again) or the report is gone, the chat just gets no live messages
    }
  }
}
