import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { firstValueFrom, of, Subject } from 'rxjs';
import { MessageDTO } from '../../DTOs/message/message-dto';
import { MessageApiService } from '../api/message-api.service';
import { MessageHubService } from '../hubs/message-hub.service';
import { ToastService } from '../local/toast.service';
import { AuthService } from './auth.service';
import { MessageService } from './message.service';

const message = (id: string): MessageDTO => ({
  id, reportId: 'report', senderId: 'student', senderName: 'Student',
  sentAt: '2026-11-07T12:00:00Z', content: 'Same content',
});

describe('MessageService cache', () => {
  const received = new Subject<MessageDTO>();
  const api = { getByReport: vi.fn() };
  const authenticated = signal(true);

  beforeEach(() => {
    vi.resetAllMocks();
    authenticated.set(true);
    TestBed.configureTestingModule({ providers: [
      { provide: MessageApiService, useValue: api },
      { provide: MessageHubService, useValue: {
        state: signal('connected'), onMessageReceived$: received,
        onTyping$: new Subject(), onStoppedTyping$: new Subject(),
      } },
      { provide: AuthService, useValue: { isLoggedIn: authenticated } },
      { provide: ToastService, useValue: {} },
    ] });
  });

  it('deduplicates by message ID while preserving separate messages with identical text and timestamps', async () => {
    api.getByReport.mockReturnValue(of([message('one'), message('two')]));
    const service = TestBed.inject(MessageService);
    await firstValueFrom(service.loadMore('report'));
    received.next(message('one'));
    expect(service.messages('report').map((entry) => entry.id)).toEqual(['one', 'two']);
  });

  it('cancels pending history and ignores live messages after logout', () => {
    const history = new Subject<MessageDTO[]>();
    api.getByReport.mockReturnValue(history);
    const service = TestBed.inject(MessageService);
    service.loadMore('report').subscribe();
    authenticated.set(false);
    service.reset();
    history.next([message('old')]);
    received.next(message('live'));
    expect(service.messages('report')).toEqual([]);
    expect(service.isLoading('report')).toBe(false);
  });
});
