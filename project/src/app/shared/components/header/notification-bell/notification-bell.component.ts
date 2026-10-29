import { ChangeDetectionStrategy, Component, computed, effect, ElementRef, inject, signal, untracked } from '@angular/core';
import { finalize } from 'rxjs';
import { NotificationService } from '../../../../core/services/api-services/notification.service';
import { formatFullDate, formatRelativeDate } from '../../../../core/utils/date-format';
import { notificationTypeMeta } from '../../../../core/utils/notification-utils';
import { statusTone } from '../../../../core/utils/status-utils';
import { IsVisibleDirective } from '../../../../shared/directives/is-visible.directive';

/** The bell of the header: the number of unseen notifications, and the panel with the list when it is clicked. */
@Component({
  selector: 'app-notification-bell',
  imports: [IsVisibleDirective],
  templateUrl: './notification-bell.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(document:click)': 'onDocumentClick($event)',
    '(document:keydown.escape)': 'close()',
  },
})
export class NotificationBellComponent {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly notificationService = inject(NotificationService);

  // state
  protected readonly isOpen = signal(false);
  protected readonly notifications = this.notificationService.notifications;
  protected readonly unreadCount = this.notificationService.unreadCount;
  protected readonly isLoading = this.notificationService.isLoading;
  protected readonly isMoreAvailable = this.notificationService.isMoreAvailable;
  protected readonly hasLoadFailed = this.notificationService.hasLoadFailed;

  /** the "mark all as seen" request is on its way (the list waits for it, so the pages it loads are already marked) */
  protected readonly isMarkingAsRead = signal(false);

  /** "9+" on the badge when there are more than nine */
  protected readonly badgeText = computed(() => (this.unreadCount() > 9 ? '9+' : String(this.unreadCount())));

  // template helpers
  protected readonly typeMeta = notificationTypeMeta;
  protected readonly statusTone = statusTone;
  protected readonly formatRelativeDate = formatRelativeDate;
  protected readonly formatFullDate = formatFullDate;

  /** the "mark all as seen" request failed during this opening of the panel */
  private hasMarkFailed = false;

  constructor() {
    // while the panel is open the notifications are on the screen, so they count as seen
    // (it runs again when a new one arrives while the panel is open)
    effect(() => {
      if (!this.isOpen()) {
        this.hasMarkFailed = false;
        return;
      }

      // a failed request goes back to unread, without this flag that would send it again and again
      if (this.unreadCount() === 0 || this.isMarkingAsRead() || this.hasMarkFailed) return;

      // untracked: the service reads and writes signals when it is subscribed, the effect must only depend on the ones above
      untracked(() => {
        this.isMarkingAsRead.set(true);

        this.notificationService
          .markAllAsRead()
          .pipe(finalize(() => this.isMarkingAsRead.set(false)))
          .subscribe({ error: () => (this.hasMarkFailed = true) }); // already shown as a toast
      });
    });
  }

  // methods
  protected toggle(): void {
    this.isOpen.update((open) => !open);
  }

  protected close(): void {
    this.isOpen.set(false);
  }

  /** the next page (the first one too): asked by the sentinel at the end of the list, and by the retry button */
  protected loadMore(): void {
    this.notificationService.loadMore().subscribe({ error: () => {} });
  }

  /** a click outside the bell and its panel closes the panel */
  protected onDocumentClick(event: Event): void {
    if (!this.isOpen()) return;
    if (this.host.nativeElement.contains(event.target as Node)) return;

    this.close();
  }
}
