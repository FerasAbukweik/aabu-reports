import { Injectable, signal } from '@angular/core';

/** State shared between the main layout (header + sidebar) and the page inside it */
@Injectable({ providedIn: 'root' })
export class LayoutService {
  // TODO: load the unread count from the api
  readonly notificationCount = signal(3);

  /** the sidebar is an off-canvas drawer below the lg breakpoint; this is whether it is open */
  readonly navOpen = signal(false);

  toggleNav(): void {
    this.navOpen.update((open) => !open);
  }

  closeNav(): void {
    this.navOpen.set(false);
  }
}
