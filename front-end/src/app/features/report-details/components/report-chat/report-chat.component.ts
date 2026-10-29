import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { MessageDTO, messageKey } from '../../../../core/DTOs/message/message-dto';
import { MessageService } from '../../../../core/services/api-services/message.service';
import { MessageHubService } from '../../../../core/services/hubs/message-hub.service';
import { formatFullDate, formatRelativeDate } from '../../../../core/utils/date-format';
import { IsVisibleDirective } from '../../../../shared/directives/is-visible.directive';

/** the longest message the user can send (the backend should use the same limit) */
const MAX_LENGTH = 500;

/** how far from the bottom (px) the list still counts as "at the bottom", so a new message scrolls it down */
const BOTTOM_THRESHOLD = 80;

/**
 * The live chat of a report: the old messages load page by page when the user scrolls up (GET /message),
 * the new ones arrive through the MessageHub, and the box sends through the hub.
 * It loads by itself and fails by itself (a message inside the card, no page-wide toast), so the rest of the page never depends on it.
 * It provides its own hub connection and state, which close when the component is destroyed.
 */
@Component({
  selector: 'app-report-chat',
  imports: [IsVisibleDirective],
  providers: [MessageHubService, MessageService],
  templateUrl: './report-chat.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportChatComponent {
  private readonly service = inject(MessageService);
  private readonly injector = inject(Injector);

  /** the report the conversation belongs to */
  readonly reportId = input.required<string>();

  protected readonly maxLength = MAX_LENGTH;
  protected readonly formatRelativeDate = formatRelativeDate;
  protected readonly formatFullDate = formatFullDate;
  protected readonly messageKey = messageKey;

  protected readonly messages = this.service.messages;
  protected readonly isLoading = this.service.isLoading;
  protected readonly isMoreAvailable = this.service.isMoreAvailable;
  protected readonly hasLoadFailed = this.service.hasLoadFailed;
  protected readonly isFirstPageDone = this.service.isFirstPageDone;
  protected readonly connectionState = this.service.connectionState;
  protected readonly typingNames = this.service.typingNames;

  protected readonly isSending = signal(false);
  protected readonly draft = signal('');
  protected readonly canSend = computed(
    () => this.draft().trim().length > 0 && !this.isSending() && this.connectionState() === 'connected',
  );

  /** the "load older" sentinel at the top only works after the list was scrolled to the bottom (else it would load every page) */
  protected readonly isSentinelArmed = signal(false);

  private readonly scroller = viewChild<ElementRef<HTMLElement>>('scroller');
  private readonly composer = viewChild<ElementRef<HTMLElement>>('composer');
  private readonly textarea = viewChild<ElementRef<HTMLTextAreaElement>>('textarea');

  /** the list is at the bottom, so what arrives is scrolled into view (and what is above stays where it is) */
  private stickToBottom = true;

  /** the size of the list before older messages were added on top, to keep the user on the same message */
  private beforeOlder: { height: number; top: number } | null = null;

  constructor() {
    // the report changed (or the first one): clear the box and open its chat
    effect(() => {
      const id = this.reportId();

      untracked(() => {
        this.draft.set('');
        this.isSentinelArmed.set(false);
        this.stickToBottom = true;
        this.beforeOlder = null;
        this.service.open(id);
      });
    });

    // the messages changed: keep the scroll where the user is (after the screen has the new messages)
    effect(() => {
      this.messages();
      const firstPageDone = this.isFirstPageDone();

      untracked(() =>
        afterNextRender(
          () => {
            const el = this.scroller()?.nativeElement;
            if (!el) return;

            if (this.beforeOlder) {
              // older messages were added on top: stay on the same message
              el.scrollTop = this.beforeOlder.top + (el.scrollHeight - this.beforeOlder.height);
              this.beforeOlder = null;
            } else if (this.stickToBottom) {
              el.scrollTop = el.scrollHeight;
            }

            if (firstPageDone) this.isSentinelArmed.set(true);
          },
          { injector: this.injector },
        ),
      );
    });
  }

  protected isOwn(message: MessageDTO): boolean {
    return this.service.isOwn(message);
  }

  /** "م. أحمد العموش" -> "م.أ" (the first letters of the first two words) */
  protected initials(name: string): string {
    return name
      .trim()
      .split(/\s+/)
      .slice(0, 2)
      .map((word) => word[0])
      .join('.');
  }

  /** the "message the team" button of the page header: brings the box into view and focuses it */
  focusComposer(): void {
    this.composer()?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
    this.textarea()?.nativeElement.focus({ preventScroll: true });
  }

  protected onScroll(): void {
    const el = this.scroller()?.nativeElement;
    if (!el) return;

    this.stickToBottom = el.scrollHeight - el.scrollTop - el.clientHeight <= BOTTOM_THRESHOLD;
  }

  /** the next older page: asked by the sentinel at the top of the list, and by the retry button */
  protected loadOlder(): void {
    if (this.isLoading()) return;

    const el = this.scroller()?.nativeElement;
    this.beforeOlder = el ? { height: el.scrollHeight, top: el.scrollTop } : null;

    this.service.loadMore().subscribe({
      error: () => (this.beforeOlder = null), // already shown as a toast
    });
  }

  protected reconnect(): void {
    this.service.reconnect();
  }

  protected onInput(event: Event): void {
    const value = (event.target as HTMLTextAreaElement).value;

    this.draft.set(value);
    this.service.draftChanged(value);
  }

  protected async send(event?: Event): Promise<void> {
    event?.preventDefault();

    const content = this.draft().trim();
    if (!content || !this.canSend()) return;

    this.isSending.set(true);
    const sent = await this.service.send(content);
    this.isSending.set(false);

    // on failure the toast already told why, the text stays in the box so the user can try again
    if (!sent) return;

    this.draft.set('');
    // the user's own message always scrolls into view
    this.stickToBottom = true;
  }
}
