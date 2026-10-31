import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  input,
  output,
  viewChild,
} from '@angular/core';

let nextId = 0;

/**
 * A modal dialog (native <dialog>) with the same header as the report form. The page owns the state:
 * it sets `open`, and sets it back to false when `closed` is emitted (close button, backdrop click or Escape).
 * The body is the projected content.
 */
@Component({
  selector: 'app-dialog',
  templateUrl: './dialog.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DialogComponent {
  // input
  readonly open = input(false);
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
  /** material symbol shown in the header */
  readonly icon = input('info');
  /** tailwind max-width class of the dialog */
  readonly maxWidth = input('max-w-lg');
  readonly dismissible = input(true);

  // output
  readonly closed = output<void>();

  // protected
  protected readonly titleId = `app-dialog-title-${nextId++}`;

  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    // the query is a signal, so this runs again once the element exists
    effect(() => {
      const element = this.dialog()?.nativeElement;
      if (!element) return;

      if (this.open() && !element.open) element.showModal();
      else if (!this.open() && element.open) element.close();
    });
  }

  /** Escape closes the native dialog: tell the page, so its state follows */
  protected onNativeClose(): void {
    if (this.open()) this.closed.emit();
  }

  protected onNativeCancel(event: Event): void {
    event.preventDefault();
    if (this.dismissible()) this.closed.emit();
  }

  protected onBackdropClick(event: MouseEvent): void {
    if (this.dismissible() && event.target === this.dialog()?.nativeElement) this.closed.emit();
  }
}
