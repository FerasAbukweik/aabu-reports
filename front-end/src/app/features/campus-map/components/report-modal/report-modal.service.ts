import { inject, Injectable, signal } from '@angular/core';
import { LocationDTO } from '../../../../core/DTOs/location/location-dto';
import { ToastService } from '../../../../core/services/local/toast.service';

@Injectable({ providedIn: 'root' })
export class ReportModalService {
  // DI
  private readonly _toast = inject(ToastService);

  // signals
  private readonly _isOpen = signal(false);
  private readonly _location = signal<LocationDTO | null>(null);

  readonly isOpen = this._isOpen.asReadonly();
  readonly location = this._location.asReadonly();

  // private
  private dialogEl: HTMLDialogElement | null = null;

  // methods
  init(dialogEL: HTMLDialogElement) {
    this.dialogEl = dialogEL;
  }

  open(location: LocationDTO) {
    if (!this.dialogEl) {
      this._toast.error('something went wrong viewing dialog');
      return;
    }
    this._location.set(location);
    this.dialogEl.showModal();
    this._isOpen.set(true);
  }

  close(): void {
    if (!this.dialogEl) {
      this._toast.error('something went wrong closing dialog');
      return;
    }
    this._isOpen.set(false);
    this.dialogEl.close();
  }
}
