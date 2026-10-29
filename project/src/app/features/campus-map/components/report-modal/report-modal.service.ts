import { Injectable, signal } from '@angular/core';

/**
 * Opens and closes the report dialog, and remembers which location it is for (only the id:
 * the component looks the location up, so it is never old).
 */
@Injectable({ providedIn: 'root' })
export class ReportModalService {
  private readonly _locationId = signal<string | null>(null);
  readonly locationId = this._locationId.asReadonly();

  private dialog: HTMLDialogElement | null = null;

  /** the component gives its <dialog> to the service when it is created */
  init(dialog: HTMLDialogElement): void {
    this.dialog = dialog;
  }

  open(locationId: string): void {
    this._locationId.set(locationId);
    this.dialog?.showModal();
  }

  close(): void {
    this.dialog?.close();
  }
}
