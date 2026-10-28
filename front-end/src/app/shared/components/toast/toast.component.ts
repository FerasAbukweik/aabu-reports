import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from '../../../core/services/local/toast.service';
import { Status } from '../../../core/enums/status.enum';
import { statusTone } from '../../../core/utils/status-utils';

/**
 * Renders the toasts of ToastService. Placed once in app.html, do not use it anywhere else.
 */
@Component({
  selector: 'app-toast',
  standalone: true,
  templateUrl: './toast.component.html',
  styleUrl: './toast.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ToastComponent {
  // DI
  protected readonly toastService = inject(ToastService);

  // Protected
  protected readonly statusTone = statusTone;

  /** errors and warnings interrupt the screen reader, the rest are polite */
  protected role(status: Status): 'alert' | 'status' {
    return status === Status.Error || status === Status.Warning ? 'alert' : 'status';
  }
}
