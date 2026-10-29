import { Injectable, signal, computed } from '@angular/core';
import { Status } from '../../enums/status.enum';
import { Toast, ToastOptions } from '../../interfaces/toast';

const DEFAULT_ICON: Record<Status, string> = {
  [Status.Normal]: 'notifications',
  [Status.Success]: 'check_circle',
  [Status.Info]: 'info',
  [Status.Warning]: 'warning',
  [Status.Error]: 'error',
  [Status.InProgress]: 'progress_activity',
};

/** how many toasts can be on the screen at once, the rest wait in the queue */
const MAX_VISIBLE = 4;

/** every this many ms, the last toast in the visible list is removed */
const REMOVE_EVERY_MS = 3500;

@Injectable({ providedIn: 'root' })
export class ToastService {
  /** every toast, including the ones still waiting for a free slot */
  private readonly _allToasts = signal<Toast[]>([]);

  /** what the component renders: the oldest MAX_VISIBLE toasts, newest first (newer ones wait in the queue) */
  readonly visibleToasts = computed(() => this._allToasts().slice(0, MAX_VISIBLE).reverse());

  /** how many toasts are waiting in the queue (not visible right now) */
  readonly extraCount = computed(() => Math.max(0, this._allToasts().length - MAX_VISIBLE));

  /** true while the user hovers or focuses the toasts, nothing is removed meanwhile */
  private isPause = false;

  private intervalHandle: ReturnType<typeof setInterval> | null = null;

  // ---------- helpers ----------
  normal(title: string, message = '', options?: ToastOptions): string {
    return this.show(Status.Normal, title, message, options);
  }

  success(title: string, message = '', options?: ToastOptions): string {
    return this.show(Status.Success, title, message, options);
  }

  info(title: string, message = '', options?: ToastOptions): string {
    return this.show(Status.Info, title, message, options);
  }

  inProgress(title: string, message = '', options?: ToastOptions): string {
    return this.show(Status.InProgress, title, message, options);
  }

  warning(title: string, message = '', options?: ToastOptions): string {
    return this.show(Status.Warning, title, message, options);
  }

  error(title: string, message = '', options?: ToastOptions): string {
    return this.show(Status.Error, title, message, options);
  }

  /** returns the toast id, so the caller can dismiss it early */
  show(status: Status, title: string, message = '', options: ToastOptions = {}): string {
    const toast: Toast = {
      id: crypto.randomUUID(),
      status,
      title,
      message,
      icon: options.icon ?? DEFAULT_ICON[status],
    };

    this._allToasts.update((list) => [...list, toast]);

    // no-op if the interval is already running
    this.intervalHandle ??= setInterval(() => this.tick(), REMOVE_EVERY_MS);

    return toast.id;
  }

  dismiss(id: string): void {
    this.isPause = false;
    this._allToasts.update((list) => list.filter((t) => t.id !== id));
    this.stopIntervalIfEmpty();
  }

  clear(): void {
    this.isPause = false;
    this._allToasts.set([]);
    this.stopIntervalIfEmpty();
  }

  // ---------- hover pause (called by the component) ----------
  pause(): void {
    this.isPause = true;
  }

  resume(): void {
    this.isPause = false;
  }

  // ---------- private ----------
  private tick(): void {
    // the user is reading a toast, skip this round
    if (this.isPause) return;

    // last item of the visible list = the oldest visible toast
    const last = this.visibleToasts().at(-1);
    if (last) this.dismiss(last.id);
  }

  private stopIntervalIfEmpty(): void {
    if (this._allToasts().length > 0 || !this.intervalHandle) return;

    clearInterval(this.intervalHandle);
    this.intervalHandle = null;
  }
}
